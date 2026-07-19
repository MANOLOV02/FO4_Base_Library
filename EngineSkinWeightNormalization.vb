''' <summary>
''' Replica la normalizacion de pesos de skin que ejecuta el <b>MOTOR</b> — <b>default ON</b> (gateado a FO4).
'''
''' <para><b>Esto NO es "el bug del CK".</b> Lo era en la hipotesis inicial; el RE lo refuto. <c>SkinBlend</c>
''' es la <b>MISMA funcion, instruccion por instruccion</b>, en <c>CreationKit.exe</c> y en <c>Fallout4.exe</c>
''' (mismo prologo, mismo unroll, mismos offsets relativos; la constante <c>1.0f</c> resuelve a 1.0 en los dos
''' binarios). No es una peculiaridad del editor al hornear: <b>es el comportamiento del motor</b>, el mismo
''' codigo corre en el juego.</para>
'''
''' <para><b>Si hay un bug, pero es del motor y lo ejecutan los dos binarios.</b> Calcular <c>w3 = 1 - Sum(w)</c>
''' solo tiene sentido si la intencion es que los pesos sumen exactamente 1; descartar ese slot cuando sale
''' <c>&lt;= 0</c> <b>sin renormalizar el resto</b> contradice esa misma intencion y deja la matriz escalada.
''' Es incoherente — pero es lo que ambos binarios ejecutan, y por eso se replica: la version
''' "matematicamente correcta" (renormalizada) <b>no la corre nadie</b>, ni el CK al hornear ni el juego en
''' runtime, asi que replicarla nos acerca a lo que el jugador ve en pantalla.</para>
'''
''' <para><b>Que es.</b> Al mezclar las matrices de skin, el motor <b>no
''' renormaliza</b> los pesos: el 4º peso del vértice <b>no se lee</b> del buffer, se <b>calcula</b> como
''' <c>w3 = 1.0f − (w0+w1+w2)</c>, y si el resultado es <c>≤ 0</c> ese slot se <b>descarta sin
''' renormalizar</b> el resto. Como w0..w2 vienen cuantizados a <c>half</c>, su suma queda a menudo apenas
''' por encima de 1 ⇒ el peso efectivo del vértice es <c>s = 1+δ</c> (δ medido hasta 3,66e-4) y la matriz
''' mezclada sale escalada por <c>s</c>. En el bake de FaceGen eso produce un residuo
''' <c>drift ≈ ε·(p+T)</c> con <c>ε = s_src/s_dst − 1</c>; como la traslación de los huesos base está
''' dominada por <c>T_z ≈ 120,84</c>, el drift es estructuralmente <b>Z-only</b>.</para>
'''
''' <para><b>FUENTE (RE, VAs exactos).</b> Función <c>SkinBlend</c>:</para>
''' <list type="bullet">
''' <item><description><c>CreationKit.exe</c> (FO4) <b>0x142B73230</b></description></item>
''' <item><description><c>Fallout4.exe</c> (runtime) <b>0x141837390</b> — <b>instrucción por
''' instrucción idéntica</b> a la del CK (mismo prólogo, mismo unroll, mismos offsets relativos;
''' la constante <c>1.0f</c> resuelve a 1.0 en ambos binarios)</description></item>
''' </list>
''' <para>Instrucciones clave (VAs del CK / del runtime):</para>
''' <list type="bullet">
''' <item><description><c>142B732E8</c> / <c>141837448</c> — <c>xor ecx, ecx</c>: el índice de arranque es
''' siempre 0 ⇒ el slot <b>calculado</b> es siempre el 3.</description></item>
''' <item><description><c>142B732F0,73347,7339E,733F5</c> / <c>141837450,…</c> — <c>cmp ecx,3 / 2 / 1 /
''' test ecx,ecx</c>: selecciona qué slot se calcula en vez de leerse.</description></item>
''' <item><description><c>142B73307-7330A</c> / <c>141837467-14183746A</c> — <c>movaps xmm0, xmm7 (=1.0f) ;
''' subss xmm0, xmm4 (=Σw)</c>: <b>w3 = 1.0 − Σ(w0..w2)</b>, en <b>precisión simple</b>.</description></item>
''' <item><description><c>142B7330E</c> / <c>14183746E</c> — <c>comiss xmm0, xmm8 (=0) ; jbe skip</c>:
''' si el peso es <b>≤ 0 se descarta</b>, y <b>no hay renormalización</b> en ninguna parte de la función
''' (ver el epílogo <c>142B73454-73529</c>: sale directo al store, sin dividir por Σw).</description></item>
''' <item><description><c>142B73327/7332E/73339</c> / <c>141837487/14183748E/141837499</c> —
''' <c>mulps/addps</c> sobre 64 B por hueso: el blend es sobre las <b>matrices</b>, no sobre el vértice.
''' </description></item>
''' </list>
''' <para>Ruta de uso en el bake del CK: <c>ApplyCustomizationRemap 0x142B6F740</c> llama a SkinBlend dos
''' veces (<c>142B6F8CA</c> invert=0 → mundo con los pesos del <c>_faceBones</c>; <c>142B6F91E</c> invert=1
''' → local con <c>CustomizationRemapData</c> y la paleta del mesh destino). El runtime tiene el
''' equivalente exacto en <c>0x141834ED0</c> (mismas dos llamadas, mismo delta 0x54).</para>
'''
''' <para><b>Por que esta ON por defecto, y por que la rama OFF sigue existiendo.</b> Esta ON porque es lo
''' que ejecuta el motor (ver arriba) y la medicion lo respalda: sobre el corpus FO4 completo (1507 NPCs /
''' 17.121 shapes vs la referencia del CK del BA2) la categoria <c>positions</c> baja de 330 NPCs / 367 shapes
''' a 301 / 334, los shapes byte-exactos suben de 1.473 a 1.482, y <b>ninguna categoria sube</b>.
''' La rama OFF (<c>Enabled = False</c>) se mantiene a proposito: es el <b>control de regresion</b> — con
''' False el camino es <b>bit-identico</b> al historico (verificado: la corrida OFF reprodujo exactamente
''' 1.473/17.121 byte-exactos y 330/367, los mismos numeros medidos antes de que esta ley existiera) — y es
''' la via de escape si aparece un caso donde convenga. <b>No eliminarla.</b></para>
'''
''' <para><b>Ojo con la metrica.</b> "Shape byte-exacto" NO es el criterio de exito de esta ley: es un AND
''' sobre todos los vertices del shape, satura, y la simulacion IDEAL tampoco produce shapes byte-exactos
''' nuevos (queda ~2,7 % de vertices a ~1 ULP de half, que es irreducible). Las metricas que discriminan son
''' <b>vertices exactos</b>, el <b>histograma en ULP de half</b> y la <b>banda 0,02</b>.</para>
'''
''' <para><b>⛔ Gate por juego.</b> El mecanismo está verificado por RE <b>sólo en FO4</b>
''' (CreationKit.exe + Fallout4.exe). En Skyrim <b>NO está verificado</b>: la firma de bytes de SkinBlend
''' no aparece en <c>SkyrimSE.exe</c> ni en su <c>CreationKit.exe</c>, y ninguno de los strings ancla del
''' bake de FaceGen de FO4 existe en el CK de Skyrim ⇒ codegen y/o ruta distintas. <b>Ausencia de patrón
''' no prueba ausencia de comportamiento</b>, así que SSE queda <b>fuera</b> hasta que se verifique por RE
''' propio. El único punto que enciende <see cref="Enabled"/> debe gatear por
''' <c>Config_App.Game_Enum.Fallout4</c>.</para>
'''
''' <para><b>Contrato de sincronía.</b> Esta ley se aplica en los mismos puntos que el blend normal:
''' render CPU (<c>SkinningHelper.BlendBoneMatrices</c> + el loop de <c>ExtractSkinnedGeometry</c>),
''' render GPU (los pesos que se suben en <c>GPUBoneWeights</c> — el shader ya suma sin dividir, así que
''' basta con escribir ahí los pesos de la ley), y bake (<c>SkinBakeMath</c> + el loop inverso de
''' <c>FaceGenBuildPipeline</c>). RENDER == BAKE.</para>
''' </summary>
Public Module EngineSkinWeightNormalization

    ''' <summary>Gate global. <b>False por defecto</b> = comportamiento normalizado de siempre, bit-idéntico.
    ''' Sólo debe encenderse para FO4 (ver el ⛔ del resumen de la clase).</summary>
    Public Property Enabled As Boolean = False

    ''' <summary>Cuántos slots de peso maneja la ley. El <c>SkinBlend</c> del RE lee/computa exactamente 4
    ''' (unroll de 4 con <c>add ecx,4 / cmp ecx,4</c> en <c>142B73448-7344E</c>).</summary>
    Public Const Slots As Integer = 4

    ''' <summary>Índice del slot que el CK <b>calcula</b> en vez de leer (<c>xor ecx,ecx</c> ⇒ siempre 3).</summary>
    Public Const ComputedSlot As Integer = 3

    ''' <summary>
    ''' Calcula los 4 pesos según la ley del CK y los deja en <paramref name="w"/>.
    ''' Devuelve <c>False</c> (sin tocar <paramref name="w"/>) cuando la ley NO aplica — el llamador debe
    ''' entonces seguir por su camino normalizado de siempre. No aplica si:
    ''' el gate está apagado; el layout no es de exactamente 4 pesos por vértice (el RE es 4-slot); o el
    ''' rango pedido se sale del array.
    ''' </summary>
    ''' <param name="flatWgt">Array plano de pesos en <c>half</c> (el mismo que sube al GPU / lee el bake).</param>
    ''' <param name="baseSlot">Offset del vértice dentro de <paramref name="flatWgt"/>.</param>
    ''' <param name="wpv">Pesos por vértice del shape.</param>
    ''' <param name="w">Buffer de salida de longitud ≥ 4 (lo provee el llamador para no allocar por vértice).</param>
    ''' <summary>DIAGNÓSTICO (no afecta el resultado): cuántos vértices tomaron la rama de la ley,
    ''' cuántos rechazos hubo por layout ≠ 4 slots, y en cuántos el 4º peso salió ≤ 0 (= los únicos donde
    ''' la ley DIVERGE del blend normalizado). Sin esto, "no cambió nada" es ambiguo entre "la ley no
    ''' aplica" y "la ley aplica y no mueve la aguja".</summary>
    Public Applied As Long = 0
    Public RejectedWpv As Long = 0
    Public DiscardedW3 As Long = 0

    Public Sub ResetStats()
        Applied = 0 : RejectedWpv = 0 : DiscardedW3 = 0
    End Sub

    Public Function StatsLine() As String
        Return $"[engineskinnorm] enabled={Enabled} appliedVerts={Applied} rejectedByWpv={RejectedWpv} w3<=0 (divergentes)={DiscardedW3}"
    End Function

    Public Function TryComputeWeights(flatWgt As System.Half(), baseSlot As Integer, wpv As Integer, w() As Single) As Boolean
        If Not Enabled Then Return False
        If flatWgt Is Nothing OrElse w Is Nothing OrElse w.Length < Slots Then Return False
        ' El RE es estrictamente 4-slot; con otro layout (NiSkinPartition expandido, etc.) no hay ley que replicar.
        If wpv <> Slots Then
            Threading.Interlocked.Increment(RejectedWpv)
            Return False
        End If
        If baseSlot < 0 OrElse baseSlot + Slots > flatWgt.Length Then Return False

        ' 142B732F0-7330A: los slots 0..2 se LEEN y se acumulan; el 3 se CALCULA como 1 − Σ.
        ' Aritmética en precisión SIMPLE a propósito: el RE usa addss/subss, y es justamente el
        ' redondeo a float el que decide el signo de w3 (y por lo tanto si el slot se descarta).
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

End Module
