Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclBlendSomeVerticesOperator` — type **10**, `0x1419516A0`.
'
' El `type` no sale de una nota: sale de la tabla de salto del despachador de operadores
' (`0x1418C6390`, indexada por `hclOperator.type − 1`), y la entrada 10 salta a `0x1418C61B3`, que
' resuelve los tres buffers y cae en `0x1419516A0`.
'
' ⭐⭐ LOS TRES BUFFERS, CON LA DOBLE INDIRECCIÓN, leídos en `0x1418C61C0`-`0x1418C61FB`:
'
'     arg1 = buffers[ buffers[ op.bufferIdx_A (+0x30) ].Ranura ]
'     arg2 = buffers[ buffers[ op.bufferIdx_B (+0x34) ].Ranura ]
'     arg3 = buffers[ buffers[ op.bufferIdx_C (+0x38) ].Ranura ]   ⬅ el DESTINO
'
' ⛔ Los tres campos existen en la tabla de layout (`hclBlendSomeVerticesOperator|hclOperator|40|1|
' …;bufferIdx_A,30,uint32;bufferIdx_B,34,uint32;bufferIdx_C,38,uint32;…`) pero el generador NO los
' emitió en `HkObj_` — el guión bajo del nombre se lo comió. Se leen por `Raw`, que sí los trae.
'
' ⭐ LAS DOS MATRICES SE COMPONEN UNA SOLA VEZ, fuera del bucle (`0x141951752`-`0x141951900`):
'
'     M_A = Componer(A.AEspacioDeSimulacion, destino.DesdeEspacioDeSimulacion)
'     M_B = Componer(B.AEspacioDeSimulacion, destino.DesdeEspacioDeSimulacion)
'
' (`Componer(a, b)` = las filas de `a` transformadas por `b`; la fila 3 con `TransformarPunto`
' —`addps xmm7, xmm2` en `0x14195181B`— y las otras tres sin traslación.)
'
' Y por cada entrada de 8 B `{vertexIndex u16 @0, blendWeight real @4}`:
'
'     t     = bcast(blendWeight)                    ' 0x1419519CE/D5
'     un_t  = bcast(1,0 − blendWeight)              ' 0x1419519CA/D9/E3, el 1,0 de 0x142929458
'     out[v] = (P_B[v] · M_B) · un_t + (P_A[v] · M_A) · t     ' 0x1419519E7/EA/ED
'
' ⛔ CUATRO CANALES, cada uno con su bandera y su propio bucle:
'   · posiciones  — siempre; `TransformarPunto` (la traslación entra, `0x1419519AB`)
'   · normales    — `op.blendNormals`     (+0x3C, `0x141951B51`), canal `+0x38`/`+0x44`/`+0x48`
'   · tangentes   — `op.blendTangents`    (+0x3D, `0x141951DCC`), canal `+0x50`/`+0x5C`/`+0x60`
'   · bitangentes — `op.blendBitangents`  (+0x3E, `0x141952058`), canal `+0x68`/`+0x74`/`+0x78`
' Los tres últimos van con `TransformarDireccion` — **sin** traslación (`0x141951BF2`-`0x141951C28`:
' tres términos y ningún `addps` de la fila 3).
'
' ⛔ La escritura es un `movups` de **16 bytes** (`0x1419519F0`, `0x141951C58`, `0x141951EEB`), no
' los 12 de los canales del skin: acá el destino se direcciona con el stride del buffer.
'
' ⚠️ CERO apariciones en el corpus vanilla. Va igual: es de la lista cerrada.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>`hclBlendSomeVerticesOperator` — type 10, `0x1419516A0`.</summary>
    Friend NotInheritable Class OpMezclarAlgunos
        Inherits OperadorCompilado

        Private ReadOnly _bufferA As Integer, _bufferB As Integer, _bufferDestino As Integer
        Private ReadOnly _vertice As Integer(), _peso As Single()
        Private ReadOnly _normales As Boolean, _tangentes As Boolean, _bitangentes As Boolean

        Friend Sub New(bufferA As Integer, bufferB As Integer, bufferDestino As Integer,
                       vertice As Integer(), peso As Single(),
                       normales As Boolean, tangentes As Boolean, bitangentes As Boolean,
                       nombre As String)
            MyBase.New(10, nombre)
            _bufferA = bufferA : _bufferB = bufferB : _bufferDestino = bufferDestino
            _vertice = vertice : _peso = peso
            _normales = normales : _tangentes = tangentes : _bitangentes = bitangentes
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _vertice Is Nothing OrElse _vertice.Length = 0 Then Return
            Dim a = Buffers.Real(ctx.Buffers, _bufferA)
            Dim b = Buffers.Real(ctx.Buffers, _bufferB)
            Dim o = Buffers.Real(ctx.Buffers, _bufferDestino)

            ' las dos matrices, UNA sola vez (`0x141951752`-`0x141951900`)
            Dim mA = Operadores.Componer(a.AEspacioDeSimulacion, o.DesdeEspacioDeSimulacion)
            Dim mB = Operadores.Componer(b.AEspacioDeSimulacion, o.DesdeEspacioDeSimulacion)

            For i = 0 To _vertice.Length - 1
                Dim v = _vertice(i)
                If v < 0 OrElse v >= o.Cuenta OrElse v >= a.Cuenta OrElse v >= b.Cuenta Then Continue For
                Dim t = Vector128.Create(_peso(i))                          ' 0x1419519CE/D5
                Dim unT = Vector128.Create(1.0F - _peso(i))                 ' 0x1419519CA/D9/E3

                ' ---- posiciones: `TransformarPunto`, la traslacion ENTRA (`0x1419519AB`)
                Dim pa = Operadores.TransformarPunto(a.Vertice(v), mA)
                Dim pb = Operadores.TransformarPunto(b.Vertice(v), mB)
                o.SetVertice(v, Vector128.Add(Vector128.Multiply(pb, unT),
                                              Vector128.Multiply(pa, t)))   ' 0x1419519E7/EA/ED

                ' ---- normales (`+0x3C`, `0x141951B51`) — y los tres canales de direccion van
                ' SIN traslacion (`0x141951BF2`-`0x141951C28`: tres terminos y ningun `addps` de F3)
                If _normales AndAlso a.Normales IsNot Nothing AndAlso b.Normales IsNot Nothing AndAlso
                   o.Normales IsNot Nothing Then
                    Dim na = Operadores.TransformarDireccion(a.Normal(v), mA)
                    Dim nb = Operadores.TransformarDireccion(b.Normal(v), mB)
                    o.SetNormal(v, Vector128.Add(Vector128.Multiply(nb, unT),
                                                 Vector128.Multiply(na, t)))
                End If

                ' ---- tangentes (`+0x3D`, `0x141951DCC`)
                If _tangentes AndAlso a.Tangentes IsNot Nothing AndAlso b.Tangentes IsNot Nothing AndAlso
                   o.Tangentes IsNot Nothing Then
                    Dim ta = Operadores.TransformarDireccion(a.Tangente(v), mA)
                    Dim tb = Operadores.TransformarDireccion(b.Tangente(v), mB)
                    o.SetTangente(v, Vector128.Add(Vector128.Multiply(tb, unT),
                                                   Vector128.Multiply(ta, t)))
                End If

                ' ---- bitangentes (`+0x3E`, `0x141952058`)
                If _bitangentes AndAlso a.Bitangentes IsNot Nothing AndAlso
                   b.Bitangentes IsNot Nothing AndAlso o.Bitangentes IsNot Nothing Then
                    Dim ba = Operadores.TransformarDireccion(a.Bitangente(v), mA)
                    Dim bb = Operadores.TransformarDireccion(b.Bitangente(v), mB)
                    o.SetBitangente(v, Vector128.Add(Vector128.Multiply(bb, unT),
                                                     Vector128.Multiply(ba, t)))
                End If
            Next
        End Sub

    End Class

End Namespace

#End If
