Imports System.Runtime.CompilerServices

''' <summary>El catálogo de tintes de cara que el compositor tiene EN JUEGO para una raza y un
''' género.
'''
''' <para>No es el modelo de un record. Un tinte en juego puede venir de dos lugares que no se
''' parecen: del RACE -las capas Male/Female Tint Layers, que se leen de la vista del árbol- o de un
''' archivo de LooksMenu (<c>Data\F4SE\Plugins\F4EE\Tints\...</c>), que no tiene record detrás y no
''' tiene dónde colgarse en el árbol. Superponer uno sobre otro es una OPERACIÓN -la hace
''' <c>LmCustomTintLoader.Fusionar</c>- y lo que sale de ahí es este modelo, que es del
''' compositor.</para>
'''
''' <para>Por eso vive acá y no entre los records: el que declara el tipo es quien puede tener
''' entradas que ningún archivo del juego declara.</para></summary>
Public Class ColorDeTinteEfectivo
    Public ColorFormID As UInteger
    Public Alpha As Single
    Public TemplateIndex As UShort
    Public BlendOperation As UInteger
End Class

''' <summary>Qué clase de tinte es, que es lo que decide cómo lo compone el motor.</summary>
Public Enum ClaseDeTinte
    ''' <summary>Una textura + blendOp: máscara en escala de grises tintada por un color uniforme
    ''' del TEND.</summary>
    Mask = 0
    ''' <summary>Textura de gradiente + arreglo de colores CLFM: el color lo elige el índice de
    ''' plantilla.</summary>
    Palette = 1
    ''' <summary>Material completo (diffuse+normal+specular) ya coloreado: el TEND sólo lleva
    ''' intensidad.</summary>
    TextureSet = 2
End Enum

''' <summary>Una opción de tinte en juego, venga del RACE o de LooksMenu.</summary>
Public Class OpcionDeTinteEfectiva
    Public Slot As UShort
    Public Index As UShort
    Public Name As String = ""
    Public Flags As UShort
    Public Textures As New List(Of String)
    Public BlendOperation As UInteger
    Public HasBlendOperation As Boolean
    Public TemplateColors As New List(Of ColorDeTinteEfectivo)
    Public DefaultValue As Single
    Public HasDefaultValue As Boolean

    ''' <summary>Verdadero cuando la opción la trajo un tint CUSTOM de LooksMenu
    ''' (<c>Data\F4SE\Plugins\F4EE\Tints\&lt;plugin&gt;\templates.json</c>) y no el record. El
    ''' editor la usa para marcar la fila "[LM]". Siempre falsa para las que salen del RACE, así que
    ''' es inerte mientras no haya tints custom instalados.</summary>
    Public EsDeLooksMenu As Boolean = False

    ''' <summary>Clase de la opción. La de un RACE se deduce de su estructura -eso hace
    ''' <see cref="TintesEfectivos.ClasificarPorEstructura"/>-; la de un tint de LooksMenu la
    ''' DECLARA el archivo, que es lo único que distingue un TextureSet que sólo trae Diffuse de una
    ''' Mask.</summary>
    Public EntryType As ClaseDeTinte = ClaseDeTinte.Mask
End Class

''' <summary>Una categoría de tintes en juego: el grupo del RACE, o la categoría que declara
''' LooksMenu.</summary>
Public Class GrupoDeTinteEfectivo
    Public GroupName As String = ""
    Public Options As New List(Of OpcionDeTinteEfectiva)
    Public CategoryIndex As UInteger
End Class

''' <summary>Armado y consulta del catálogo de tintes en juego.</summary>
Public Module TintesEfectivos

    ''' <summary>Los tintes que declara el RACE para ese género. Es la mitad "de fábrica" del
    ''' catálogo: lo que agrega LooksMenu se superpone después, y esa superposición no vuelve al
    ''' árbol.
    ''' <para>Las capas de los dos géneros son clases generadas distintas, pero declaran la misma
    ''' forma, así que el recorrido es UNO solo: se elige la lista y de ahí en más todo sale por la
    ''' interfaz de forma. Elegirla con un ternario entre las dos listas no compila a lo que parece
    ''' -son genéricos de tipos distintos-, así que va con If/Else.</para></summary>
    <Extension>
    Public Function TintesDelRecord(fo4 As Canon.RaceFO4,
                                    isFemale As Boolean) As List(Of GrupoDeTinteEfectivo)
        Dim result As New List(Of GrupoDeTinteEfectivo)
        If fo4 Is Nothing Then Return result

        Dim capas As IEnumerable(Of Canon.IBloque_TintLayers)
        If isFemale Then
            capas = fo4.FemaleTintLayers
        Else
            capas = fo4.MaleTintLayers
        End If

        For Each g In capas
            Dim grupo As New GrupoDeTinteEfectivo With {
                .GroupName = g.GroupName, .CategoryIndex = g.GroupCategoryIndex}
            For Each o In g.Options
                grupo.Options.Add(OpcionDelRecord(o))
            Next
            result.Add(grupo)
        Next
        Return result
    End Function

    ''' <summary>Una opción tal como la declara el RACE. Lo que el record no trae no se inventa: el
    ''' blend y el valor por defecto viajan con su presencia, porque "no lo declara" y "lo declara
    ''' en cero" son dos cosas distintas para el compositor.</summary>
    Private Function OpcionDelRecord(o As Canon.IBloque_Options) As OpcionDeTinteEfectiva
        Dim opt As New OpcionDeTinteEfectiva With {
            .Slot = o.IndexSlot, .Index = o.OptionIndex, .Name = o.OptionName,
            .Flags = o.OptionFlags,
            .HasBlendOperation = o.OptionBlendOperationPresente,
            .HasDefaultValue = o.OptionDefaultPresente}
        If opt.HasBlendOperation Then opt.BlendOperation = o.OptionBlendOperation
        If opt.HasDefaultValue Then opt.DefaultValue = o.OptionDefault
        For Each t In o.Textures
            opt.Textures.Add(t.Texture)
        Next
        For Each c In o.TemplateColors
            opt.TemplateColors.Add(New ColorDeTinteEfectivo With {
                .ColorFormID = c.TemplateColorColor, .Alpha = c.TemplateColorAlpha,
                .TemplateIndex = c.TemplateColorTemplateIndex,
                .BlendOperation = c.TemplateColorBlendOperation})
        Next
        opt.EntryType = ClasificarPorEstructura(opt)
        Return opt
    End Function

    ''' <summary>Clasifica por la estructura del subrecord. Medido sobre HumanRace (162 opciones
    ''' femeninas, 131 masculinas): los grupos se separan perfecto con la cantidad de texturas y de
    ''' colores sola.
    ''' <para>Tres texturas y ningún color es TextureSet (tripletes diffuse+normal+specular, slots
    ''' FaceDetail/Scars/Brow). Una textura y algún color es Palette (una máscara de gradiente más
    ''' el arreglo TTEC, slots de maquillaje y pintura). Una textura y ningún color es Mask
    ''' (selectores de región anatómica, slots 0 a 6).</para>
    ''' <para>El TTED NO discrimina: en HumanRace todas las Palette también lo traen.</para>
    ''' </summary>
    Public Function ClasificarPorEstructura(opt As OpcionDeTinteEfectiva) As ClaseDeTinte
        If opt Is Nothing Then Return ClaseDeTinte.Mask
        If opt.Textures.Count >= 2 Then Return ClaseDeTinte.TextureSet
        If opt.TemplateColors.Count > 0 Then Return ClaseDeTinte.Palette
        Return ClaseDeTinte.Mask
    End Function

    ''' <summary>Busca una opción por su índice TETI en el catálogo YA fusionado (record +, si
    ''' corresponde, los tints custom de LooksMenu).</summary>
    <Extension>
    Public Function BuscarOpcion(grupos As IEnumerable(Of GrupoDeTinteEfectivo),
                                 index As UShort) As OpcionDeTinteEfectiva
        If grupos Is Nothing Then Return Nothing
        For Each g In grupos
            For Each o In g.Options
                If o.Index = index Then Return o
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>Todas las opciones de ese slot TETI, en el catálogo YA fusionado.</summary>
    <Extension>
    Public Function BuscarOpcionesPorSlot(grupos As IEnumerable(Of GrupoDeTinteEfectivo),
                                          slot As TintSlot) As List(Of OpcionDeTinteEfectiva)
        Dim result As New List(Of OpcionDeTinteEfectiva)
        If grupos Is Nothing Then Return result
        For Each g In grupos
            For Each o In g.Options
                If o.Slot = CUShort(slot) Then result.Add(o)
            Next
        Next
        Return result
    End Function

End Module
