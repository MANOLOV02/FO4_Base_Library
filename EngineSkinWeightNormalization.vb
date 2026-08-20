''' <summary>
''' Replica la normalizacion de pesos de skin que ejecuta el MOTOR. Gateado a FO4.
'''
''' <para><b>La ley.</b> Al mezclar las matrices de skin el motor <b>no renormaliza</b>: el 4o peso no se
''' lee del buffer, se calcula como <c>w3 = 1.0f - (w0+w1+w2)</c>, y si sale <c>&lt;= 0</c> ese slot se
''' descarta <b>sin renormalizar</b> el resto. Como w0..w2 vienen cuantizados a <c>half</c>, la suma
''' efectiva queda en <c>s = 1+d</c> (d hasta 3,66e-4) y la matriz mezclada sale escalada por s.</para>
'''
''' <para><b>No es un bug del CK.</b> <c>SkinBlend</c> es la misma funcion instruccion por instruccion en
''' <c>CreationKit.exe 0x142B73230</c> y en <c>Fallout4.exe 0x141837390</c>: es el comportamiento del
''' motor, y por eso se replica. La version renormalizada "matematicamente correcta" no la corre nadie.</para>
'''
''' <para><b>Gate por juego.</b> Verificado por RE <b>solo en FO4</b>. En Skyrim NO esta verificado (la
''' firma de <c>SkinBlend</c> no aparece en sus binarios), y ausencia de patron no prueba ausencia de
''' comportamiento ⇒ SSE queda fuera. Quien encienda <see cref="Enabled"/> debe gatear por
''' <c>Config_App.Game_Enum.Fallout4</c>.</para>
'''
''' <para><b>Contrato de sincronia (RENDER == BAKE).</b> Cableada en render CPU
''' (<c>SkinningHelper.BlendBoneMatrices</c> + <c>ExtractSkinnedGeometry</c>), render GPU (los pesos que
''' se suben en <c>GPUBoneWeights</c>; el shader ya suma sin dividir) y bake (<c>SkinBakeMath</c> + el
''' loop inverso de <c>FaceGenBuildPipeline</c>).</para>
'''
''' <para><b>En el render CPU la ley es matematicamente INERTE</b> (medido): <c>SkinningHelper</c> arma
''' <c>MposeBlend · inv(MbindBlend)</c>, un cociente de dos mezclas con los MISMOS pesos, asi que el
''' <c>Sw</c> se cancela solo. Donde SI tiene efecto es en el bake, porque forward e inverso usan pesos
''' distintos (<c>_faceBones</c> vs destino) y <c>e = s_src/s_dst - 1</c> no se cancela.</para>
'''
''' <para><b>ABIERTO, no medido:</b> si el motor en GPU skinnea <c>S wk·Mk·v</c> directo (sin cociente
''' pose/bind) el <c>Sw</c> no se cancelaria y nuestro render CPU divergiria. No afirmarlo en ninguna
''' direccion hasta medirlo.</para>
''' </summary>
Public Module EngineSkinWeightNormalization

    ''' <summary>Gate global. <c>False</c> = comportamiento normalizado de siempre.
    ''' Solo debe encenderse para FO4 (ver el gate por juego en el resumen de la clase).</summary>
    Public Property Enabled As Boolean = False

    ''' <summary>Slots de peso que maneja la ley. El <c>SkinBlend</c> del RE lee/computa exactamente 4.</summary>
    Public Const Slots As Integer = 4

    ''' <summary>Indice del slot que el motor CALCULA en vez de leer (<c>xor ecx,ecx</c> ⇒ siempre 3).</summary>
    Public Const ComputedSlot As Integer = 3

    ' Diagnostico: distingue "la ley no aplica" de "la ley aplica y no mueve la aguja".
    ' DiscardedW3 cuenta los unicos vertices donde la ley DIVERGE del blend normalizado.
    Public Applied As Long = 0
    Public RejectedWpv As Long = 0
    Public DiscardedW3 As Long = 0

    Public Sub ResetStats()
        Applied = 0 : RejectedWpv = 0 : DiscardedW3 = 0
    End Sub

    Public Function StatsLine() As String
        Return $"[engineskinnorm] enabled={Enabled} appliedVerts={Applied} rejectedByWpv={RejectedWpv} w3<=0 (divergentes)={DiscardedW3}"
    End Function

    ''' <summary>
    ''' Calcula los 4 pesos segun la ley del motor y los deja en <paramref name="w"/>.
    ''' Devuelve <c>False</c> (sin tocar <paramref name="w"/>) cuando la ley NO aplica y el llamador debe
    ''' seguir por su camino normalizado: gate apagado, layout distinto de 4 pesos por vertice, o rango
    ''' fuera del array.
    ''' </summary>
    ''' <param name="flatWgt">Array plano de pesos en <c>half</c> (el mismo que sube al GPU / lee el bake).</param>
    ''' <param name="baseSlot">Offset del vertice dentro de <paramref name="flatWgt"/>.</param>
    ''' <param name="wpv">Pesos por vertice del shape.</param>
    ''' <param name="w">Buffer de salida de longitud >= 4 (lo provee el llamador para no allocar por vertice).</param>
    Public Function TryComputeWeights(flatWgt As System.Half(), baseSlot As Integer, wpv As Integer, w() As Single) As Boolean
        If Not Enabled Then Return False
        If flatWgt Is Nothing OrElse w Is Nothing OrElse w.Length < Slots Then Return False
        ' El RE es estrictamente 4-slot; con otro layout (NiSkinPartition expandido, etc.) no hay ley que replicar.
        If wpv <> Slots Then
            Threading.Interlocked.Increment(RejectedWpv)
            Return False
        End If
        If baseSlot < 0 OrElse baseSlot + Slots > flatWgt.Length Then Return False

        ' Aritmetica en precision SIMPLE a proposito: el RE usa addss/subss, y es el redondeo a float
        ' el que decide el signo de w3 (y por lo tanto si el slot se descarta).
        Dim acc As Single = 0.0F
        For j = 0 To ComputedSlot - 1
            w(j) = CSng(flatWgt(baseSlot + j))
            acc += w(j)
        Next
        w(ComputedSlot) = 1.0F - acc
        Threading.Interlocked.Increment(Applied)
        If w(ComputedSlot) <= 0.0F Then Threading.Interlocked.Increment(DiscardedW3)
        Return True
    End Function

    ''' <summary>
    ''' Restituye el ancla homogenea (<c>M44 = 1</c>) de una matriz que salio de una mezcla ponderada.
    ''' <b>Llamar SIEMPRE antes de invertir un <c>Mtot</c> de skinning.</b>
    '''
    ''' <para><b>Por que.</b> El idioma <c>Mtot += mat * peso</c> escala los 16 elementos, pero solo 12
    ''' describen la transformacion: <c>M44</c> es el ancla, no geometria, y tras la mezcla queda
    ''' <c>M44 = S pesos</c>. <c>Vector3d.TransformPosition</c> nunca divide por w ⇒ el forward sale bien;
    ''' <c>Matrix4d.Invert</c> hace algebra homogenea completa ⇒ mete un <c>1/Sw</c> que cancela
    ''' exactamente el <c>e</c> que esta ley existe para producir. El motor mezcla un 3x4 y su fila 3
    ''' queda <c>[0,0,0,1]</c>: no puede contaminar <c>M44</c> ni queriendo.</para>
    '''
    ''' <para>Se aplica en <b>todas</b> las ramas, tambien con la ley apagada: ahi el
    ''' <c>Mtot x (1/sumW)</c> deja un residuo de coma flotante (<c>M44 = 0,999999999421803</c>), el mismo
    ''' defecto seis ordenes mas chico. Eso rompe a proposito la bit-identidad historica de la rama OFF.</para>
    '''
    ''' <para><b>No lo "arregles" en <c>SkinningHelper.BlendBoneMatrices</c></b>: ahi el cociente
    ''' <c>MposeBlend · inv(MbindBlend)</c> cancela el <c>Sw</c> solo (M44 = 1,000000000000; diferencia
    ''' con/sin re-anclaje = 1,4e-14). No hay nada que arreglar y tocarlo es riesgo sin beneficio.</para>
    ''' </summary>
    Public Function ReanchorAffine(m As OpenTK.Mathematics.Matrix4d) As OpenTK.Mathematics.Matrix4d
        m.M44 = 1.0
        Return m
    End Function

End Module
