Imports OpenTK.Mathematics

''' <summary>⭐⭐ Las matrices de skin per-vertice, guardadas en SoA (12 arrays planos) en vez de en un
''' <c>Matrix4()</c>.
'''
''' <para>⛔ POR QUE. El kernel de skinning (<see cref="FastSkin"/>) es memory-bound y para vectorizar
''' necesita los datos en SoA — todos los M11 juntos, todos los M12 juntos. Con un array de structs hay que
''' COPIAR a arrays planos antes de operar (VB no tiene Span ni MemoryMarshal), y esa copia cuesta MAS que
''' lo que el ancho de vector ahorra. Guardandolos en SoA desde el vamos, la copia desaparece.</para>
'''
''' <para>⭐ MEDIDO antes de escribir esto (<c>ShadowGate --soa --heavy</c>, 3 corridas, 11 mallas /
''' 37.321 vertices):
''' <list type="bullet">
''' <item>D — el camino de produccion AoS→AoS: <b>0,85 ms</b></item>
''' <item>A — el staging AoS→SoA que este cambio ELIMINA: 0,92 ms</item>
''' <item>B — la aritmetica sola SoA→SoA vectorial: <b>0,48 ms</b></item>
''' <item>B' — la misma escalar: 1,23 ms ⇒ el kernel vectorial rinde <b>2,4x</b></item>
''' </list>
''' O sea el techo es 0,48 contra 0,85 = <b>~40 % mas rapido</b>, y lo que perdia era el envoltorio, no el
''' kernel. La medicion incluso SUBESTIMA a SoA (ver la nota de sesgo en SoaBench).</para>
'''
''' <para>⛔⛔ POR QUE ES UNA CLASE CON INDEXADOR Y NO 12 ARRAYS SUELTOS. Hay 78 call sites de
''' <c>PerVertexSkinMatrix</c> repartidos en 11 archivos —incluidos el bake, el exportador de NIF y tres
''' herramientas— y casi todos siguen el mismo patron: <c>Dim mats = geo.PerVertexSkinMatrix</c> y despues
''' <c>mats(i)</c>. Exponiendo un <c>Default Property</c> que reconstruye la <c>Matrix4</c>, TODOS esos
''' siguen compilando y funcionando sin tocarse, y el kernel —el unico que necesita velocidad— lee
''' <see cref="Secciones"/> directo. Cambiar 78 sitios a mano para ganar lo mismo habria sido un riesgo
''' enorme a cambio de nada.</para>
'''
''' <para>⚠️ EL PRECIO: cada <c>mats(i)</c> reconstruye una Matrix4 (12 lecturas dispersas). Lo pagan el
''' world-cache, el bake y el exportador, que ya gastan MUCHO mas por vertice (una inversa 3x3, varias
''' normalizaciones, I/O). El que no lo paga es el bucle que domina el frame.</para>
'''
''' <para>⛔ SOLO SE GUARDAN 12 DE LOS 16 ELEMENTOS. La cuarta columna de una matriz de skin es siempre
''' (0,0,0,1) — es un blend de matrices afines con pesos que suman 1, y el arnes lo MIDIO sobre el corpus
''' (check <c>matriz-afin</c>) en vez de suponerlo. Si algun dia dejara de serlo, ese check se pone rojo
''' ANTES de que esto pierda datos en silencio.</para></summary>
Public NotInheritable Class SkinMatricesSoA

    ''' <summary>Cantidad de vertices.</summary>
    Public ReadOnly Count As Integer

    ''' <summary>Las 12 secciones: M11 M12 M13 · M21 M22 M23 · M31 M32 M33 · M41 M42 M43.
    ''' <para>⛔ El orden importa y es el mismo que espera <see cref="FastSkin"/>. No reordenar sin tocar
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

    ''' <summary>Reemplaza a <c>Array.Fill</c>: todos los vertices con la misma matriz.</summary>
    Public Sub Llenar(m As Matrix4)
        For i = 0 To Count - 1
            Me(i) = m
        Next
    End Sub

End Class
