Imports OpenTK.Mathematics

''' <summary>Las matrices de skin per-vertice, guardadas en SoA (12 arrays planos) en vez de en un
''' <c>Matrix4()</c>.
'''
''' <para>POR QUE. El kernel de skinning (<see cref="FastSkin"/>) es memory-bound y para vectorizar
''' necesita los datos en SoA — todos los M11 juntos, todos los M12 juntos. Con un array de structs hay que
''' COPIAR a arrays planos antes de operar (VB no tiene Span ni MemoryMarshal), y esa copia cuesta MAS que
''' lo que el ancho de vector ahorra. Guardandolos en SoA desde el vamos, la copia desaparece.</para>
'''
''' <para>MEDIDO (<c>ShadowGate --soa --heavy</c>, 3 corridas, 11 mallas / 37.321 vertices):
''' <list type="bullet">
''' <item>D — el camino de produccion AoS→AoS: <b>0,85 ms</b></item>
''' <item>A — el staging AoS→SoA que este cambio ELIMINA: 0,92 ms</item>
''' <item>B — la aritmetica sola SoA→SoA vectorial: <b>0,48 ms</b></item>
''' <item>B' — la misma escalar: 1,23 ms ⇒ el kernel vectorial rinde <b>2,4x</b></item>
''' </list>
''' O sea el techo es 0,48 contra 0,85 = <b>~40 % mas rapido</b>, y lo que perdia era el envoltorio, no el
''' kernel. La medicion incluso SUBESTIMA a SoA (ver la nota de sesgo en SoaBench).</para>
'''
''' <para>POR QUE ES UNA CLASE CON INDEXADOR Y NO 12 ARRAYS SUELTOS. <c>PerVertexSkinMatrix</c> tiene
''' decenas de call sites repartidos por la libreria, el bake, el exportador de NIF y las herramientas, y
''' casi todos siguen el mismo patron: <c>Dim mats = geo.PerVertexSkinMatrix</c> y despues <c>mats(i)</c>.
''' Con un <c>Default Property</c> que reconstruye la <c>Matrix4</c>, TODOS siguen compilando y funcionando
''' sin tocarse, y el kernel —el unico que necesita velocidad— lee <see cref="Secciones"/> directo.</para>
'''
''' <para>EL PRECIO: cada <c>mats(i)</c> reconstruye una Matrix4 (12 lecturas dispersas). Lo pagan el
''' world-cache, el bake y el exportador, que ya gastan MUCHO mas por vertice (una inversa 3x3, varias
''' normalizaciones, I/O). El que no lo paga es el bucle que domina el frame.</para>
'''
''' <para>SOLO SE GUARDAN 12 DE LOS 16 ELEMENTOS. La cuarta columna de una matriz de skin es siempre
''' (0,0,0,1) — es un blend de matrices afines con pesos que suman 1, y el arnes lo MIDIO sobre el corpus
''' (check <c>matriz-afin</c>) en vez de suponerlo. Si algun dia dejara de serlo, ese check se pone rojo
''' ANTES de que esto pierda datos en silencio.</para></summary>
Public NotInheritable Class SkinMatricesSoA

    ''' <summary>Cantidad de vertices.</summary>
    Public ReadOnly Count As Integer

    ''' <summary>Las 12 secciones: M11 M12 M13 · M21 M22 M23 · M31 M32 M33 · M41 M42 M43.
    ''' <para>El orden importa y es el mismo que espera <see cref="FastSkin"/>. No reordenar sin tocar
    ''' el kernel: ahi los indices estan escritos a mano por velocidad.</para></summary>
    Friend ReadOnly Secciones(11)() As Single

    Public Sub New(n As Integer)
        Count = Math.Max(0, n)
        For i = 0 To 11
            Secciones(i) = New Single(Math.Max(0, Count - 1)) {}
        Next
    End Sub

    ''' <summary>Mismo nombre que el de un array, para que los call sites que hacian <c>mats.Length</c>
    ''' no cambien.</summary>
    Public ReadOnly Property Length As Integer
        Get
            Return Count
        End Get
    End Property

    ''' <summary>Indexador. Reconstruye la Matrix4 al leer y la desarma al escribir, de modo que
    ''' <c>mats(i)</c> y <c>mats(i) = m</c> siguen significando lo mismo que con el array de structs.</summary>
    Default Public Property Item(i As Integer) As Matrix4
        Get
            Return New Matrix4(Secciones(0)(i), Secciones(1)(i), Secciones(2)(i), 0.0F,
                               Secciones(3)(i), Secciones(4)(i), Secciones(5)(i), 0.0F,
                               Secciones(6)(i), Secciones(7)(i), Secciones(8)(i), 0.0F,
                               Secciones(9)(i), Secciones(10)(i), Secciones(11)(i), 1.0F)
        End Get
        Set(value As Matrix4)
            Secciones(0)(i) = value.M11 : Secciones(1)(i) = value.M12 : Secciones(2)(i) = value.M13
            Secciones(3)(i) = value.M21 : Secciones(4)(i) = value.M22 : Secciones(5)(i) = value.M23
            Secciones(6)(i) = value.M31 : Secciones(7)(i) = value.M32 : Secciones(8)(i) = value.M33
            Secciones(9)(i) = value.M41 : Secciones(10)(i) = value.M42 : Secciones(11)(i) = value.M43
        End Set
    End Property

    ''' <summary>Escribe las 12 secciones DIRECTO desde una <c>Matrix4d</c>, sin materializar la
    ''' <c>Matrix4</c> intermedia.
    ''' <para>Lo usa el BLEND, que es el bucle mas caliente del skinning de CPU (11,85 ms de un frame de
    ''' 17,4 sobre el Serena Battle Suit). El camino largo era <c>Matrix4d</c> (128 B, devuelta por valor)
    ''' → <c>AMatrix4</c> → <c>Matrix4</c> (64 B) → indexador → 12 escrituras: dos copias de struct por
    ''' vertice para terminar guardando los mismos 12 Single.</para>
    ''' <para>BIT A BIT IDENTICO: es el mismo <c>CSng</c> sobre los mismos Double. Lo unico que cambia es
    ''' cuantas veces se copia el struct por el camino.</para>
    ''' <para>EL <c>AggressiveInlining</c> DE ACA NO APORTA NADA — medido: con y sin el atributo el blend
    ''' completo da 5,07/5,08/5,08 contra 5,11/5,13 ms, o sea ruido. El JIT ya inlinea estos cuerpos. Quien
    ''' busque los ~2,4 ms que faltan tiene que mirar el batching, no esto.</para></summary>
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub EstablecerDesde(i As Integer, m As Matrix4d)
        Secciones(0)(i) = CSng(m.M11) : Secciones(1)(i) = CSng(m.M12) : Secciones(2)(i) = CSng(m.M13)
        Secciones(3)(i) = CSng(m.M21) : Secciones(4)(i) = CSng(m.M22) : Secciones(5)(i) = CSng(m.M23)
        Secciones(6)(i) = CSng(m.M31) : Secciones(7)(i) = CSng(m.M32) : Secciones(8)(i) = CSng(m.M33)
        Secciones(9)(i) = CSng(m.M41) : Secciones(10)(i) = CSng(m.M42) : Secciones(11)(i) = CSng(m.M43)
    End Sub

    ''' <summary>Copia los 12 elementos utiles desde un acumulador plano de 16 Single (el layout de
    ''' <c>FastGeom.MatSingles</c>). Es el camino corto del blend: los datos YA estan en Single y en el
    ''' orden correcto, asi que no hay conversion ni struct intermedio.
    ''' <para>El mapeo salta las columnas 4 (indices 3, 7, 11, 15), que son la parte proyectiva y no se
    ''' guarda — la matriz de skin es afin, y eso lo verifica el check [matriz-afin] del arnes.</para>
    ''' </summary>
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Sub CopiarDeAcumulador(i As Integer, acc As Single())
        Secciones(0)(i) = acc(0) : Secciones(1)(i) = acc(1) : Secciones(2)(i) = acc(2)
        Secciones(3)(i) = acc(4) : Secciones(4)(i) = acc(5) : Secciones(5)(i) = acc(6)
        Secciones(6)(i) = acc(8) : Secciones(7)(i) = acc(9) : Secciones(8)(i) = acc(10)
        Secciones(9)(i) = acc(12) : Secciones(10)(i) = acc(13) : Secciones(11)(i) = acc(14)
    End Sub

    ''' <summary>Copia la matriz del vertice <paramref name="desde"/> al <paramref name="i"/>.
    ''' <para>Lo usa el memo por vertice-previo del blend: cuando dos vertices consecutivos tienen la
    ''' misma tupla (indices, pesos) el resultado es identico por construccion —el blend es funcion pura de
    ''' esa tupla y de la paleta— y copiar 12 floats sale mucho mas barato que rehacerlo.</para></summary>
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Sub CopiarDeVertice(i As Integer, desde As Integer)
        Secciones(0)(i) = Secciones(0)(desde) : Secciones(1)(i) = Secciones(1)(desde) : Secciones(2)(i) = Secciones(2)(desde)
        Secciones(3)(i) = Secciones(3)(desde) : Secciones(4)(i) = Secciones(4)(desde) : Secciones(5)(i) = Secciones(5)(desde)
        Secciones(6)(i) = Secciones(6)(desde) : Secciones(7)(i) = Secciones(7)(desde) : Secciones(8)(i) = Secciones(8)(desde)
        Secciones(9)(i) = Secciones(9)(desde) : Secciones(10)(i) = Secciones(10)(desde) : Secciones(11)(i) = Secciones(11)(desde)
    End Sub

    ''' <summary>Posicion transformada por la matriz del vertice <paramref name="i"/>, SIN construir
    ''' ninguna matriz.
    ''' <para>Existe para no pasar por <c>Vector3d.TransformPosition(v, AMatrix4d(mats(i)))</c>, que arma
    ''' DOS structs para usar 12 floats: el indexador reconstruye una <c>Matrix4</c> de 64 B desde las
    ''' secciones y despues se ensancha a <c>Matrix4d</c> de 128 B.</para>
    ''' <para>BIT A BIT IDENTICO: <c>TransformPosition</c> acumula en Double y <c>Single → Double</c> es
    ''' exacto, asi que el producto da el mismo bit. Mismo orden de acumulacion, ademas.</para>
    ''' <para>Existe como metodo publico —y no exponiendo <c>Secciones</c>— porque el exportador de NIF
    ''' vive en otro assembly y no deberia conocer el layout interno.</para></summary>
    Public Function TransformarPosicion(i As Integer, v As Vector3d) As Vector3d
        Return New Vector3d(v.X * Secciones(0)(i) + v.Y * Secciones(3)(i) + v.Z * Secciones(6)(i) + Secciones(9)(i),
                            v.X * Secciones(1)(i) + v.Y * Secciones(4)(i) + v.Z * Secciones(7)(i) + Secciones(10)(i),
                            v.X * Secciones(2)(i) + v.Y * Secciones(5)(i) + v.Z * Secciones(8)(i) + Secciones(11)(i))
    End Function

    ''' <summary>La matriz del vertice como <c>Matrix4d</c>, armada DIRECTO desde las secciones.
    ''' <para>Para los consumidores que necesitan la matriz entera (el exportador la pasa a
    ''' <c>NormalMatrixOrIdentity</c> y a <c>PorMatriz3x3</c>). Ahorra la <c>Matrix4</c> intermedia del
    ''' indexador; la <c>Matrix4d</c> se construye una sola vez en vez de dos structs.</para></summary>
    Public Function ComoMatrix4d(i As Integer) As Matrix4d
        Return New Matrix4d(Secciones(0)(i), Secciones(1)(i), Secciones(2)(i), 0.0,
                            Secciones(3)(i), Secciones(4)(i), Secciones(5)(i), 0.0,
                            Secciones(6)(i), Secciones(7)(i), Secciones(8)(i), 0.0,
                            Secciones(9)(i), Secciones(10)(i), Secciones(11)(i), 1.0)
    End Function

    ''' <summary>Reemplaza a <c>Array.Fill</c>: todos los vertices con la misma matriz.</summary>
    Public Sub Llenar(m As Matrix4)
        For i = 0 To Count - 1
            Me(i) = m
        Next
    End Sub

End Class
