' ============================================================================================
' Este archivo transcribe a mano material de las declaraciones de formato de xEdit (ordinales de
' tipo, constantes de formato, y el DSL de declaracion en si), que estan bajo Mozilla Public
' License 2.0, y por lo tanto es una obra derivada de ellas.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Ver THIRD-PARTY-NOTICES.md en la raiz del repositorio.
' ============================================================================================
Imports System.IO
Imports System.Text
Imports FO4_Base_Library

Namespace Canon

    ''' <summary>Falla de layout: los bytes no encajan en la declaración. Nunca se absorbe con un
    ''' fallback, porque un guard que tapa un desajuste convierte un error en corrupción
    ''' muda.</summary>
    Public Class WbLayoutException
        Inherits Exception
        Public ReadOnly Property Path As String
        Public Sub New(path As String, message As String)
            MyBase.New($"{path}: {message}")
            _Path = path
        End Sub
    End Class

    ''' <summary>Decisor de unión: devuelve el ÍNDICE del miembro que aplica. Recibe los bytes
    ''' crudos porque hay decisores que los inspeccionan para elegir la rama — por ejemplo el que
    ''' mira el contenido de un bloque de información de modelo.</summary>
    Public Delegate Function WbDecider(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As Integer

    ''' <summary>Contador de un array calculado por CALLBACK. Recibe el NODO del array y sube por
    ''' los contenedores hasta el que busca, así que la resolución es sobre el ÁRBOL ya parseado, no
    ''' sobre los bytes.</summary>
    Public Delegate Function WbCounter(node As WbNode) As Integer

    ''' <summary>Base de la CAPA B: una definición de valor dentro de los bytes de un subrecord.</summary>
    Public MustInherit Class WbValueDef
        Inherits WbDef

        ''' <summary>Parsea desde <paramref name="offset"/> con a lo sumo <paramref name="avail"/>
        ''' bytes disponibles. El nodo devuelto trae <c>SourceLength</c> con lo consumido.
        ''' <para>Nunca puede leer más allá de <c>offset + avail</c>: si no entra, TIRA
        ''' <see cref="WbLayoutException"/>. Eso es lo que hace visible un campo declarado un byte
        ''' más adelante de lo que corresponde, en vez de dejarlo leer del campo vecino.</para></summary>
        Public MustOverride Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode

        ''' <summary>Emite el nodo. El escritor NO mira los bytes originales: reconstruye desde el
        ''' valor tipado.</summary>
        Public MustOverride Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)

        ''' <summary>Tamaño fijo si se conoce sin mirar los datos, o -1. Es lo que consume el
        ''' discriminador POR LARGO con el que se separan dos subrecords de la misma firma.</summary>
        Public Overridable Function DefaultSize(ctx As WbContext) As Integer
            Return -1
        End Function

        ''' <summary>Crea el nodo por defecto (record NUEVO, sin bytes de origen). Sólo se usa al
        ''' CREAR un record, nunca al leer uno existente.</summary>
        Public MustOverride Function CreateDefault(ctx As WbContext) As WbNode

        ''' <summary>¿El crudo preservado de una hoja de texto sigue describiendo su valor ACTUAL?
        '''
        ''' <para><see cref="WbNode.RawOverride"/> existe para un solo caso: el texto de la fuente no
        ''' vuelve a los mismos bytes al re-codificarlo, así que se guarda la forma exacta que traía.
        ''' Vale mientras nadie TOQUE el valor. Si alguien lo editó, el crudo describe el texto VIEJO
        ''' y emitirlo tira la edición a la basura <b>sin un solo aviso</b> — era el mecanismo por el
        ''' que un nombre corregido a mano volvía a salir roto en cada guardado.</para>
        '''
        ''' <para>La pregunta se contesta con una FUNCIÓN PURA DEL ESTADO —decodificar el crudo y
        ''' compararlo con el valor— y no con una marca de "sucio". Es la misma ley que ya rige para
        ''' la glossiness de SSE: una marca depende de que exista y de por qué camino se editó, y
        ''' falla muda cuando alguien escribe el valor sin pasar por el setter previsto.</para>
        '''
        ''' <para>⛔ La comparación NO puede depender del codepage AMBIENTE. El guardado lo cambia:
        ''' <c>SaveEsp_Form</c> envuelve la escritura en un <c>PushTranslatableOverride</c>, así que la
        ''' codificación vigente al EMITIR puede no ser la que hubo al PARSEAR. Decodificando con la de
        ''' ahora, el crudo de un campo que NADIE tocó deja de coincidir y se re-codifica: bytes
        ''' cambiados en un campo que el usuario no editó, que es justo lo contrario de para qué existe
        ''' el crudo. Por eso se acepta cualquiera de las decodificaciones plausibles —la del archivo de
        ''' origen y la vigente—: un valor EDITADO no coincide con ninguna, y uno intacto coincide con la
        ''' que lo produjo.</para>
        ''' <para>Costo: uno o dos decodes por hoja QUE TENGA CRUDO. Son 218 hojas en Fallout 4 y 448 en
        ''' Skyrim sobre 1,87 millones de nodos.</para></summary>
        Protected Shared Function CrudoVigente(raw As Byte(), valorActual As String,
                                               decodificar As Func(Of Byte(), String),
                                               Optional decodificarAlterno As Func(Of Byte(), String) = Nothing) As Boolean
            If raw Is Nothing Then Return False
            Dim esperado = If(valorActual, "")
            If String.Equals(decodificar(raw), esperado, StringComparison.Ordinal) Then Return True
            If decodificarAlterno Is Nothing Then Return False
            Return String.Equals(decodificarAlterno(raw), esperado, StringComparison.Ordinal)
        End Function

        ''' <summary>Decodifica con la codificación que DECLARA el archivo de origen, o Nothing si no
        ''' declara ninguna. Es el segundo intento de <see cref="CrudoVigente"/>.</summary>
        Protected Shared Function DecodeDelOrigen(ctx As WbContext) As Func(Of Byte(), String)
            If ctx Is Nothing OrElse ctx.TranslatableEncoding Is Nothing Then Return Nothing
            Dim enc = ctx.TranslatableEncoding
            Return Function(b) enc.GetString(b, 0, b.Length)
        End Function

        Protected Function NewNode() As WbNode
            Return New WbNode(Me)
        End Function

        ''' <summary>Guard de tamaño. Recibe el NODO, no su ruta ya armada: construir
        ''' <c>WbNode.Path</c> es subir por todos los ancestros armando una lista y un
        ''' <c>String.Join</c>, y este guard está en el camino FELIZ de CADA hoja.
        ''' <para>MEDIDO: pasándole <c>n.Path</c> ya construido, ese string de diagnóstico que casi
        ''' siempre se tira era el <b>72 % del tiempo de parseo en Fallout 4 y el 98 % en Skyrim</b>
        ''' (1.610 ARMO en 98,4 ms · 7.920 ARMO en 238,8 ms, contra 24,9 y 42,7 ms del parser plano).
        ''' Y <c>ParseARMO</c> NO es código frío: lo llaman <c>NpcRenderContext</c>,
        ''' <c>OutfitResolver</c>, <c>EquipResolver</c>, <c>FaceGenBuilder</c> y
        ''' <c>NpcOverrideSaver</c>. La ruta se arma sólo cuando de verdad se tira.</para></summary>
        Protected Sub Need(node As WbNode, avail As Integer, want As Integer)
            If avail < want Then
                Throw New WbLayoutException(node.Path, $"faltan bytes: hacen falta {want}, quedan {avail}")
            End If
        End Sub
    End Class

    '======================================================================================
    ' Hojas
    '======================================================================================

    ''' <summary>Entero, con el ancho y el signo que declare su tipo.
    ''' <para>Un entero que codifica un índice de enum mide los mismos 4 bytes que una referencia y
    ''' NO es una referencia. Por eso el enum vive acá y jamás en <see cref="WbFormIdDef"/>: el
    ''' remapper de índices de master sólo recorre ese otro tipo. Es el caso de
    ''' <c>MGEF.ResistValue</c> — referencia en Fallout 4, entero con enum en Skyrim — y el de
    ''' <c>WEAP.Skill</c>.</para></summary>
    Public NotInheritable Class WbIntegerDef
        Inherits WbValueDef

        Public ReadOnly Property IntType As WbIntType
        ''' <summary>Nombre del enum o del conjunto de flags asociado, si lo hay. Documenta que el
        ''' número es un ÍNDICE, no una referencia.</summary>
        Public ReadOnly Property EnumName As String

        ''' <summary>Nombre de cada bit, indexado por número de bit. Vacío cuando el campo no es
        ''' un conjunto de banderas.
        ''' <para>Está para que preguntar por una bandera sea preguntar por su nombre en vez de
        ''' hacer una cuenta de bits en el sitio que la usa. La cuenta escrita a mano es correcta
        ''' hasta que alguien mueve un bit.</para></summary>
        Public ReadOnly Property FlagNames As String()

        ''' <summary>Nombre de cada valor posible, cuando el campo es una enumeración.</summary>
        Public ReadOnly Property EnumValues As IReadOnlyDictionary(Of Long, String)

        Public Sub New(name As String, t As WbIntType, Optional enumName As String = "",
                       Optional flagNames As String() = Nothing,
                       Optional enumValues As IReadOnlyDictionary(Of Long, String) = Nothing)
            Me.Name = name
            _IntType = t
            _EnumName = enumName
            _FlagNames = flagNames
            _EnumValues = enumValues
        End Sub

        ''' <summary>Número de bit de una bandera por su nombre, o -1 si el campo no la declara.</summary>
        Public Function BitOf(flagName As String) As Integer
            If FlagNames Is Nothing Then Return -1
            For i = 0 To FlagNames.Length - 1
                If String.Equals(FlagNames(i), flagName, StringComparison.OrdinalIgnoreCase) Then Return i
            Next
            Return -1
        End Function

        ''' <summary>Ancho en bytes de cada tipo de entero.</summary>
        Public Shared Function WidthOf(t As WbIntType) As Integer
            Select Case t
                Case WbIntType.i0 : Return 0
                Case WbIntType.u8, WbIntType.s8 : Return 1
                Case WbIntType.u16, WbIntType.s16 : Return 2
                Case WbIntType.u24 : Return 3
                Case WbIntType.u32, WbIntType.s32 : Return 4
                Case Else : Return 8
            End Select
        End Function

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return WidthOf(IntType)
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim w = WidthOf(IntType)
            Dim n = NewNode()
            n.Parent = parent
            If avail < w Then
                ' Un entero MAS CORTO de lo declarado se TOLERA: el ancho declarado es el caso
                ' normal, no una garantia del archivo. Ejemplo real: el DATA de las CELL de Skyrim
                ' declara 2 bytes de flags y hay 331 records que traen 1.
                ' Se lee lo que hay y se EMITE lo mismo: el round-trip cierra y la discrepancia
                ' queda reportada, que es la verdad — el dato viola la declaracion, no al reves.
                ' avail = 0 TAMBIEN se tolera: un subrecord de CERO bytes cuya definicion es un
                ' entero se lee como 0 y ocupa 0. Es el caso del ANAM que CIERRA cada Action de
                ' SCEN (misma firma que el ANAM 'Type' de 2 bytes que la ABRE): tirar ahi hace
                ' saltar la excepcion hasta el nivel del record y perder TODO lo que venia despues.
                If avail < 0 Then Need(n, avail, w)
                Dim acc As Long = 0
                For i = 0 To avail - 1
                    acc = acc Or (CLng(data(offset + i)) << (8 * i))
                Next
                n.Value = WbCajas.Caja(acc)
                n.SourceLength = avail
                n.ShortRead = True
                ctx.Report(WbFindingKind.Tessellation, n.Path,
                           $"entero declarado de {w} bytes con sólo {avail} en el dato")
                Return n
            End If
            n.Value = WbCajas.Caja(ReadRaw(data, offset))
            n.SourceLength = w
            Return n
        End Function

        ''' <summary>Devuelve SIEMPRE un <c>Long</c>. Los u64 se guardan reinterpretando los bits
        ''' (no hay dato de este tipo en los records modelados; la emisión los devuelve idénticos).
        ''' <para>El signo de los enteros de 8 bits se aplica a mano: <c>CSByte(b)</c> tira
        ''' <c>OverflowException</c> con b &gt; 127.</para></summary>
        Private Function ReadRaw(data As Byte(), o As Integer) As Long
            Select Case IntType
                Case WbIntType.i0 : Return 0L
                Case WbIntType.u8 : Return CLng(data(o))
                Case WbIntType.s8 : Return CLng(CInt(data(o)) - If(data(o) > 127, 256, 0))
                Case WbIntType.u16 : Return CLng(BitConverter.ToUInt16(data, o))
                Case WbIntType.s16 : Return CLng(BitConverter.ToInt16(data, o))
                Case WbIntType.u24 : Return CLng(data(o)) Or (CLng(data(o + 1)) << 8) Or (CLng(data(o + 2)) << 16)
                Case WbIntType.u32 : Return CLng(BitConverter.ToUInt32(data, o))
                Case WbIntType.s32 : Return CLng(BitConverter.ToInt32(data, o))
                Case Else : Return BitConverter.ToInt64(data, o)
            End Select
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            Dim v = Convert.ToInt64(node.Value)
            If node.ShortRead Then
                ' Se re-emite EXACTAMENTE la cantidad de bytes que traia la fuente.
                For i = 0 To node.SourceLength - 1
                    bw.Write(CByte((v >> (8 * i)) And &HFFL))
                Next
                Return
            End If
            Select Case IntType
                Case WbIntType.i0
                    ' ancho 0: no ocupa bytes
                    Exit Select
                Case WbIntType.u8, WbIntType.s8
                    bw.Write(CByte(v And &HFFL))
                Case WbIntType.u16, WbIntType.s16
                    bw.Write(CUShort(v And &HFFFFL))
                Case WbIntType.u24
                    bw.Write(CByte(v And &HFFL))
                    bw.Write(CByte((v >> 8) And &HFFL))
                    bw.Write(CByte((v >> 16) And &HFFL))
                Case WbIntType.u32, WbIntType.s32
                    bw.Write(CUInt(v And &HFFFFFFFFL))
                Case Else
                    bw.Write(v)
            End Select
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            n.Value = WbCajas.Caja(0L)
            n.SourceLength = WidthOf(IntType)
            Return n
        End Function
    End Class

    ''' <summary>Float de 32 bits.</summary>
    Public NotInheritable Class WbFloatDef
        Inherits WbValueDef

        Public Sub New(name As String)
            Me.Name = name
        End Sub

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return 4
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Need(n, avail, 4)
            n.Value = WbCajas.Caja(BitConverter.ToSingle(data, offset))
            n.SourceLength = 4
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            bw.Write(CSng(node.Value))
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            n.Value = WbCajas.Caja(0.0F)
            n.SourceLength = 4
            Return n
        End Function
    End Class

    ''' <summary>Referencia a otro record.
    ''' <para><see cref="AllowedSignatures"/> es la lista de firmas a las que el campo puede apuntar
    ''' (por ejemplo <c>[ARMA, NULL]</c>). La pseudo-firma <c>FFFF</c> declara que
    ''' <c>0xFFFFFFFF</c> es el CENTINELA "ninguno/todos" y NO una referencia rota: no se
    ''' remapea.</para>
    ''' <para>Este es el ÚNICO tipo que el remapper de índices de master toca. Un entero que sea un
    ''' índice de enum no puede llegar ahí ni por accidente.</para></summary>
    Public NotInheritable Class WbFormIdDef
        Inherits WbValueDef

        Public ReadOnly Property AllowedSignatures As String()

        Public Sub New(name As String, ParamArray allowed As String())
            Me.Name = name
            _AllowedSignatures = If(allowed, Array.Empty(Of String)())
        End Sub

        ''' <summary>El campo declara <c>FFFF</c> entre sus destinos permitidos.</summary>
        Public ReadOnly Property AllowsSentinel As Boolean
            Get
                Return AllowedSignatures.Contains("FFFF")
            End Get
        End Property

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return 4
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Need(n, avail, 4)
            n.Value = WbCajas.Caja(BitConverter.ToUInt32(data, offset))
            n.SourceLength = 4
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            bw.Write(CUInt(node.Value))
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            n.Value = WbCajas.Caja(0UI)
            n.SourceLength = 4
            Return n
        End Function
    End Class

    ''' <summary>Cadena. <paramref name="fixedLength"/> = 0 ⇒ terminada en NUL (zstring); &gt; 0 ⇒
    ''' ancho fijo.
    ''' <para><see cref="Translatable"/> distingue las dos rutas de encoding: los campos de texto
    ''' visible para el jugador se decodifican con el codepage de traducción del archivo, y el resto
    ''' con el codepage general. El EDID NO es traducible.</para></summary>
    Public NotInheritable Class WbStringDef
        Inherits WbValueDef

        Public ReadOnly Property FixedLength As Integer
        Public ReadOnly Property Translatable As Boolean

        Public Sub New(name As String, Optional fixedLength As Integer = 0, Optional translatable As Boolean = False)
            Me.Name = name
            _FixedLength = fixedLength
            _Translatable = translatable
        End Sub

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return If(FixedLength > 0, FixedLength, -1)
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Dim textLen As Integer
            Dim total As Integer
            If FixedLength > 0 Then
                Need(n, avail, FixedLength)
                textLen = FixedLength
                total = FixedLength
                n.TerminatorCount = 0
            Else
                ' Texto terminado en cero: el texto llega hasta el primer cero, y ese cero es el
                ' unico byte que el campo consume de mas.
                '
                ' Tragarse todos los ceros seguidos parece inofensivo y no lo es: si el campo que
                ' viene despues es un booleano o un contador que vale cero, el texto se lo come y
                ' ese campo se queda sin bytes. Es lo que pasaba con la marca de colision de los
                ' escombros cada vez que valia falso.
                Dim i = offset
                Dim limit = offset + avail
                While i < limit AndAlso data(i) <> 0
                    i += 1
                End While
                textLen = i - offset
                Dim terms = If(i < limit, 1, 0)
                n.TerminatorCount = terms
                total = textLen + terms
            End If
            n.Value = Decode(data, offset, textLen, ctx)
            n.SourceLength = total
            ' Guarda de fidelidad: si el texto decodificado NO vuelve a los mismos bytes, el
            ' encoding no es round-trippable para esta secuencia. Se conserva el crudo y se
            ' CUENTA (WbReport.EncodingFallbacks) — nunca se copia en silencio.
            Dim reenc = Encode(CStr(n.Value), ctx)
            If Not BytesEqual(reenc, data, offset, textLen) Then
                Dim raw(textLen - 1) As Byte
                If textLen > 0 Then Buffer.BlockCopy(data, offset, raw, 0, textLen)
                n.RawOverride = raw
                ' Aviso al contenedor de que ACÁ hubo un crudo. Sin esto, el subrecord que envuelve a
                ' esta hoja tiene que recorrer todo su subárbol para averiguarlo, y lo haría SIEMPRE
                ' — también en el 99,9 % de los subrecords donde no hay ningún texto. Ver
                ' WbSubrecordDef.Parse. Sólo lo incrementa ESTA clase, que es la única cuyo nodo
                ' cuenta para ese aviso (el filtro de allá es `TypeOf leaf.Def Is WbStringDef`).
                ctx.TextosCrudos += 1
            End If
            Return n
        End Function

        Private Shared Function BytesEqual(a As Byte(), data As Byte(), offset As Integer, len As Integer) As Boolean
            If a.Length <> len Then Return False
            For i = 0 To len - 1
                If a(i) <> data(offset + i) Then Return False
            Next
            Return True
        End Function

        Private Function Decode(data As Byte(), o As Integer, len As Integer, ctx As WbContext) As String
            If len <= 0 Then Return ""
            If Translatable Then
                Return PluginEncodingSettings.DecodeTranslatable(data, o, len)
            End If
            Return PluginEncodingSettings.DecodeGeneral(data, o, len)
        End Function

        Private Function Encode(s As String, ctx As WbContext) As Byte()
            If String.IsNullOrEmpty(s) Then Return Array.Empty(Of Byte)()
            If Translatable Then
                Return PluginEncodingSettings.EncodeTranslatable(s)
            End If
            Return PluginEncodingSettings.EncodeGeneral(s)
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            Dim texto = CStr(node.Value)
            Dim body As Byte() = If(CrudoVigente(node.RawOverride, texto,
                                                 Function(b) Decode(b, 0, b.Length, ctx),
                                                 If(Translatable, DecodeDelOrigen(ctx), Nothing)),
                                    node.RawOverride, Encode(texto, ctx))
            If FixedLength > 0 Then
                Dim buf(FixedLength - 1) As Byte
                Buffer.BlockCopy(body, 0, buf, 0, Math.Min(body.Length, FixedLength))
                bw.Write(buf)
            Else
                bw.Write(body)
                For i = 1 To node.TerminatorCount
                    bw.Write(CByte(0))
                Next
            End If
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            n.Value = ""
            If FixedLength > 0 Then
                n.SourceLength = FixedLength
            Else
                n.TerminatorCount = 1
                n.SourceLength = 1
            End If
            Return n
        End Function
    End Class

    ''' <summary>Cadena localizable: si el archivo FUENTE tiene el flag 0x80 (Localized) el campo
    ''' son 4 bytes con el ID de la cadena en las tablas de idioma; si no, es una zstring inline.
    ''' <para>Por eso "" y AUSENTE no son lo mismo: en un master localizado un DESC con id 0 resuelve
    ''' a texto vacío y el subrecord SÍ está. Acá esa distinción es estructural: el subrecord
    ''' ausente no tiene nodo.</para></summary>
    Public NotInheritable Class WbLStringDef
        Inherits WbValueDef

        Public Sub New(name As String)
            Me.Name = name
        End Sub

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            If ctx.Localized Then
                Need(n, avail, 4)
                n.Value = WbCajas.Caja(CLng(BitConverter.ToUInt32(data, offset)))
                n.SourceLength = 4
                Return n
            End If
            Dim i = offset
            Dim limit = offset + avail
            While i < limit AndAlso data(i) <> 0
                i += 1
            End While
            Dim textLen = i - offset
            ' Un solo cero de cierre, igual que en el texto no traducible: tragarse los ceros
            ' siguientes le come los bytes al campo que viene detras cuando ese campo vale cero.
            Dim terms = If(i < limit, 1, 0)
            n.TerminatorCount = terms
            n.Value = If(textLen > 0, PluginEncodingSettings.DecodeTranslatable(data, offset, textLen), "")
            n.SourceLength = textLen + terms
            Dim reenc = If(textLen > 0, PluginEncodingSettings.EncodeTranslatable(CStr(n.Value)), Array.Empty(Of Byte)())
            If reenc.Length <> textLen Then
                Dim raw(textLen - 1) As Byte
                If textLen > 0 Then Buffer.BlockCopy(data, offset, raw, 0, textLen)
                n.RawOverride = raw
            Else
                For k = 0 To textLen - 1
                    If reenc(k) <> data(offset + k) Then
                        Dim raw(textLen - 1) As Byte
                        Buffer.BlockCopy(data, offset, raw, 0, textLen)
                        n.RawOverride = raw
                        Exit For
                    End If
                Next
            End If
            Return n
        End Function

        ''' <summary>Emite el campo con la forma que pide el archivo DESTINO, que no tiene por qué
        ''' ser la del archivo de origen.
        ''' <list type="bullet">
        ''' <item>id → destino con tablas: se escribe el id. Es el round-trip byte-exacto.</item>
        ''' <item><b>id → destino SIN tablas: se resuelve el id y se escribe la zstring.</b> Es el
        ''' caso que faltaba: escribir los 4 bytes del id en un archivo que declara texto deja un
        ''' nombre que todo lector —el juego, xEdit y la propia aplicación— lee como basura.</item>
        ''' <item>texto → destino sin tablas: se escribe el texto.</item>
        ''' <item>texto → destino con tablas: TIRA. Habría que dar de alta la cadena en la tabla del
        ''' archivo y emitir su id; la aplicación no escribe archivos localizados, así que ese camino
        ''' no existe y no se inventa.</item>
        ''' </list></summary>
        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            Dim esId = EsIdDeTabla(node, ctx)

            ' Comparar o medir no es grabar: no hay archivo destino, así que cada campo sale con la
            ' forma que el nodo tiene guardada. Ver WbContext.Comparando.
            If ctx.Comparando Then
                If esId Then
                    bw.Write(CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL))
                Else
                    bw.Write(PluginEncodingSettings.EncodeTranslatable(CStr(node.Value)))
                    bw.Write(CByte(0))
                End If
                Return
            End If

            Dim destinoLocalizado = If(ctx.DestinoLocalizado.HasValue, ctx.DestinoLocalizado.Value, ctx.Localized)
            If destinoLocalizado Then
                If Not esId Then
                    Throw New InvalidOperationException(
                        $"{ctx.RecordSignature}\{Name}: el destino usa tablas de idioma y el campo tiene TEXTO. " &
                        "Escribirlo pide dar de alta la cadena en la tabla del archivo y emitir su identificador; " &
                        "ese camino no está implementado porque la aplicación no genera archivos localizados.")
                End If
                bw.Write(CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL))
                Return
            End If

            If esId Then
                ' El destino no tiene tablas: acá el campo ES una zstring, y el terminador va SIEMPRE
                ' (el nodo viene de 4 bytes sin terminador, así que su TerminatorCount es cero).
                bw.Write(PluginEncodingSettings.EncodeTranslatable(TextoDelId(node, ctx)))
                bw.Write(CByte(0))
                Return
            End If

            Dim literal = CStr(node.Value)
            Dim body As Byte() = If(CrudoVigente(node.RawOverride, literal,
                                                 Function(b) PluginEncodingSettings.DecodeTranslatable(b, 0, b.Length),
                                                 DecodeDelOrigen(ctx)),
                                    node.RawOverride, PluginEncodingSettings.EncodeTranslatable(literal))
            bw.Write(body)
            For i = 1 To node.TerminatorCount
                bw.Write(CByte(0))
            Next
        End Sub

        ''' <summary>Si el VALOR de la hoja es un identificador de tabla.
        '''
        ''' <para>⛔ El estado del nodo se consulta, no se REPITE. Al leer no se estampa nada: un campo
        ''' recién parseado dice exactamente lo que dice su archivo, y eso ya está en el contexto. Es la
        ''' regla de xEdit —<c>TwbLStringDef</c> deja el elemento en <c>tbUnknown</c> y cae a
        ''' <c>_File.IsLocalized</c>; sólo <c>FromStringNative</c> marca, o sea sólo al ASIGNAR—. El
        ''' estado existe para registrar la EXCEPCIÓN: "este campo ya no es lo que su archivo declara".</para>
        '''
        ''' <para>Estampar en el parseo daba el mismo resultado y costaba memoria de verdad: el valor
        ''' estampado nunca es el default, así que forzaba el objeto de extras —que existe justamente para
        ''' que lo paguen sólo los nodos que se apartan del default— en TODA hoja localizable de TODO
        ''' record. Ver el bloque de <c>WbNodeExtras</c>.</para>
        '''
        ''' <para>Es <b>Public</b> porque la tienen que contestar igual el emisor y los arneses. Con dos
        ''' implementaciones, el gate y el escritor podrían discrepar y el gate no serviría para nada.</para></summary>
        Public Shared Function EsIdDeTabla(node As WbNode, ctx As WbContext) As Boolean
            Select Case node.ValorLocalizado
                Case WbLocalizacion.IdDeTabla : Return True
                Case WbLocalizacion.Texto : Return False
                Case Else : Return ctx.Localized
            End Select
        End Function

        ''' <summary>Texto de un identificador, para materializarlo en un destino sin tablas.
        ''' <para>El identificador CERO significa "sin texto" y no es un fallo. Cualquier otro que no
        ''' se pueda resolver deja el campo VACÍO y queda REPORTADO: el archivo sale bien formado y el
        ''' hallazgo dice qué NPC se quedó sin nombre.</para></summary>
        Private Function TextoDelId(node As WbNode, ctx As WbContext) As String
            Dim id As UInteger = 0UI
            Try
                id = CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL)
            Catch
            End Try
            If id = 0UI Then Return ""
            Dim texto As String = Nothing
            If ctx.ResolverTextoLocalizado IsNot Nothing Then texto = ctx.ResolverTextoLocalizado(node)
            If String.IsNullOrEmpty(texto) Then
                ctx.Report(WbFindingKind.TextoLocalizadoSinResolver, node.Path,
                           $"identifier 0x{id:X8} unresolved against the language tables: the field is written EMPTY")
                Return ""
            End If
            Return texto
        End Function

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            If ctx.Localized Then
                n.Value = WbCajas.Caja(0L)
                n.SourceLength = 4
            Else
                n.Value = ""
                n.TerminatorCount = 1
                n.SourceLength = 1
            End If
            Return n
        End Function
    End Class

    ''' <summary>Bloque de bytes de tamaño declarado. <paramref name="size"/> &lt;= 0 ⇒ "el resto".
    ''' <para>NO es un blob: un bloque de bytes o un campo de relleno son declaraciones EXPLÍCITAS,
    ''' con nombre y tamaño, para lo que se sabe que ocupa lugar y todavía no se desarmó campo por
    ''' campo. Lo que está prohibido es copiar un subrecord entero sin declararlo — eso se marca
    ''' como PENDIENTE y se REPORTA.</para></summary>
    Public NotInheritable Class WbByteArrayDef
        Inherits WbValueDef

        Public ReadOnly Property Size As Integer

        Public Sub New(name As String, Optional size As Integer = -1)
            Me.Name = name
            _Size = size
        End Sub

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return If(Size > 0, Size, -1)
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Dim take = If(Size > 0, Size, avail)
            If take > avail Then
                ' El dato trae menos bytes que los declarados: se lee lo que hay, se re-emite igual
                ' y queda reportado. Caso real: el relleno de 32 bytes del LGTM de Skyrim llega
                ' con 24.
                ctx.Report(WbFindingKind.Tessellation, n.Path,
                           $"byte array declarado de {take} con sólo {avail} en el dato")
                take = avail
            End If
            Need(n, avail, take)
            Dim buf(Math.Max(take - 1, -1)) As Byte
            If take > 0 Then Buffer.BlockCopy(data, offset, buf, 0, take)
            n.Value = If(take > 0, buf, Array.Empty(Of Byte)())
            n.SourceLength = take
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            Dim b = TryCast(node.Value, Byte())
            If b IsNot Nothing AndAlso b.Length > 0 Then bw.Write(b)
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            Dim take = Math.Max(Size, 0)
            Dim buf(Math.Max(take - 1, -1)) As Byte
            n.Value = If(take > 0, buf, Array.Empty(Of Byte)())
            n.SourceLength = take
            Return n
        End Function
    End Class

    ''' <summary>Cero bytes. Es la rama "campo ausente" de las uniones que dependen de la Form
    ''' Version del record.</summary>
    Public NotInheritable Class WbEmptyDef
        Inherits WbValueDef

        Public Sub New(name As String)
            Me.Name = name
        End Sub

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Return 0
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            n.SourceLength = 0
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Return NewNode()
        End Function
    End Class

    ''' <summary>Cadena con PREFIJO de longitud (por defecto 4 bytes). No lleva NUL: el largo lo da
    ''' el prefijo.
    ''' <para>Es el '3D Name' de una textura alternativa, o sea el contenido de los
    ''' <c>MO2S</c>/<c>MO4S</c> de SKYRIM — que en Fallout 4 son un FormID a un material swap. Otro
    ''' struct GAME-DEPENDENT dentro del mismo ARMO.</para></summary>
    Public NotInheritable Class WbLenStringDef
        Inherits WbValueDef

        Public ReadOnly Property PrefixWidth As Integer
        ''' <summary>Charset con el que se decodifica. Hay campos (los del bloque de scripts) que
        ''' declaran uno propio, y eso cambia sólo el texto: NADA del tamaño.</summary>
        Public ReadOnly Property Encoding As WbTextEncoding

        Public Sub New(name As String, Optional prefixWidth As Integer = 4,
                       Optional enc As WbTextEncoding = WbTextEncoding.General)
            Me.Name = name
            _PrefixWidth = prefixWidth
            _Encoding = enc
        End Sub

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Need(n, avail, PrefixWidth)
            Dim len As Integer
            Select Case PrefixWidth
                Case 4 : len = CInt(BitConverter.ToUInt32(data, offset))
                Case 2 : len = CInt(BitConverter.ToUInt16(data, offset))
                Case Else : len = CInt(data(offset))
            End Select
            Need(n, avail - PrefixWidth, len)
            Dim body As Byte() = Array.Empty(Of Byte)()
            If len > 0 Then
                ReDim body(len - 1)
                Buffer.BlockCopy(data, offset + PrefixWidth, body, 0, len)
            End If
            n.Value = Decode(body)
            Dim reenc = Encode(CStr(n.Value))
            If reenc.Length <> len Then
                n.RawOverride = body
            Else
                For k = 0 To len - 1
                    If reenc(k) <> body(k) Then
                        n.RawOverride = body
                        Exit For
                    End If
                Next
            End If
            n.SourceLength = PrefixWidth + len
            Return n
        End Function

        Private Function Decode(b As Byte()) As String
            If b.Length = 0 Then Return ""
            Select Case Encoding
                Case WbTextEncoding.Translatable : Return PluginEncodingSettings.DecodeTranslatable(b, 0, b.Length)
                Case WbTextEncoding.Vmad : Return PluginEncodingSettings.DecodeGeneral(b, 0, b.Length)
                Case Else : Return PluginEncodingSettings.DecodeGeneral(b, 0, b.Length)
            End Select
        End Function

        Private Function Encode(s As String) As Byte()
            If String.IsNullOrEmpty(s) Then Return Array.Empty(Of Byte)()
            Select Case Encoding
                Case WbTextEncoding.Translatable : Return PluginEncodingSettings.EncodeTranslatable(s)
                Case WbTextEncoding.Vmad : Return PluginEncodingSettings.EncodeVmad(s)
                Case Else : Return PluginEncodingSettings.EncodeGeneral(s)
            End Select
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            Dim texto = CStr(node.Value)
            Dim body As Byte() = If(CrudoVigente(node.RawOverride, texto, Function(b) Decode(b),
                                                 If(Encoding = WbTextEncoding.Translatable, DecodeDelOrigen(ctx), Nothing)),
                                    node.RawOverride, Encode(texto))
            Select Case PrefixWidth
                Case 4 : bw.Write(CUInt(body.Length))
                Case 2 : bw.Write(CUShort(body.Length))
                Case Else : bw.Write(CByte(body.Length))
            End Select
            If body.Length > 0 Then bw.Write(body)
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            n.Value = ""
            n.SourceLength = PrefixWidth
            Return n
        End Function
    End Class

    '======================================================================================
    ' Contenedores de la capa B
    '======================================================================================

    ''' <summary>Struct de valores: secuencia ORDENADA de miembros cuyos offsets se DERIVAN de los
    ''' tamaños. No hay offsets literales en ningún lado.
    ''' <para>Es lo que hace imposible un desplazamiento silencioso de un campo: para leer un miembro
    ''' un byte más adelante hay que INSERTAR un byte, y entonces el struct mide uno más de lo que
    ''' mide el dato y salen reportados todos los records de ese tipo. El offset no es editable por
    ''' separado.</para></summary>
    Public NotInheritable Class WbStructDef
        Inherits WbValueDef

        Public ReadOnly Property Members As WbValueDef()

        ''' <summary>Índice desde el cual los miembros son OPCIONALES: si se acabaron los bytes, el
        ''' struct termina ahí y eso es legal.
        ''' <para>Caso real: el BODT de Skyrim declara opcional su <c>Armor Type</c>, así que un BODT
        ''' de 8 bytes es válido. Sin modelar esto se marcarían truncados un montón de records
        ''' CORRECTOS.</para>
        ''' <para>-1 = ningún miembro es opcional.</para></summary>
        Public Property OptionalFromElement As Integer = -1

        Public Sub New(name As String, members As WbValueDef())
            Me.Name = name
            _Members = members
            For Each m In members
                If m IsNot Nothing Then m.DefParent = Me
            Next
        End Sub

        Public Function OptionalFrom(idx As Integer) As WbStructDef
            OptionalFromElement = idx
            Return Me
        End Function

        ''' <summary>Suma de los miembros. Con <see cref="OptionalFromElement"/> es un MÁXIMO, no un
        ''' tamaño exacto; por eso un struct con miembros opcionales no debe usarse con SizeMatch.</summary>
        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            Dim total = 0
            For Each m In Members
                Dim s = m.DefaultSize(ctx)
                If s < 0 Then Return -1
                total += s
            Next
            Return total
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Dim pos = offset
            Dim left = avail
            For i = 0 To Members.Length - 1
                If OptionalFromElement >= 0 AndAlso i >= OptionalFromElement AndAlso left <= 0 Then Exit For
                ' NO se corta por "left <= 0": hay miembros que legítimamente ocupan CERO bytes (un
                ' array de cuenta 0, un campo vacío, un entero de ancho 0). Cortar ahí da un falso
                ' positivo en todos los MODT de 20 bytes, cuyos cuatro contadores valen 0 y cierran
                ' exacto. Se corta SÓLO si el miembro de verdad pide bytes que no están.
                Dim child As WbNode
                Try
                    child = Members(i).Parse(ctx, data, pos, left, n)
                Catch ex As WbLayoutException
                    If i = 0 Then Throw
                    ctx.Report(WbFindingKind.Tessellation, n.Path,
                               $"el struct cortó en el miembro '{Members(i).Name}': {ex.Message}")
                    Exit For
                End Try
                n.AddChild(child)
                pos += child.SourceLength
                left -= child.SourceLength
            Next
            n.SourceLength = pos - offset
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            For i = 0 To node.Children.Count - 1
                Dim c = node.Children(i)
                CType(c.Def, WbValueDef).Emit(c, bw, ctx)
            Next
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            For Each m In Members
                n.AddChild(m.CreateDefault(ctx))
            Next
            Return n
        End Function
    End Class

    ''' <summary>Array de valores.
    ''' <para><b><see cref="Count"/> codifica de dónde sale la cantidad de elementos:</b></para>
    ''' <list type="bullet">
    ''' <item><c>&gt; 0</c> ⇒ cantidad FIJA.</item>
    ''' <item><c>= 0</c> ⇒ se repite hasta agotar los bytes.</item>
    ''' <item><c>= -1</c> ⇒ PREFIJO de conteo u32 dentro de los datos.</item>
    ''' <item><c>= -2</c> ⇒ prefijo u16. <c>= -4</c> ⇒ prefijo u8.</item>
    ''' </list>
    ''' <para>Un array con prefijo puede además traer NOMBRES para sus elementos, y esos nombres NO
    ''' son la cantidad: en el bloque de contadores de un modelo, el prefijo u32 dice cuántos
    ''' contadores vienen y la lista de nombres ('Textures', 'Addon Nodes', …) sólo los etiqueta.
    ''' Leerlo como "cuatro contadores fijos" desfasa todo el resto del MODT.</para>
    ''' <para><see cref="CountPath"/> ⇒ la cantidad la manda OTRO nodo, al que se llega por una ruta.
    ''' Al ESCRIBIR, ese nodo se RECALCULA desde la longitud real del array ⇒ el contador no puede
    ''' desincronizarse.</para></summary>
    Public NotInheritable Class WbArrayDef
        Inherits WbValueDef

        Public ReadOnly Property Element As WbValueDef
        Public ReadOnly Property Count As Integer
        Public ReadOnly Property CountPath As String
        ''' <summary>Nombres de los elementos cuando el array los declara uno por uno ('Textures',
        ''' 'Addon Nodes', …).</summary>
        Public ReadOnly Property ElementNames As String()

        ''' <summary>Contador por CALLBACK: no es un prefijo en el dato ni un hermano con el número,
        ''' es una FUNCIÓN sobre el árbol ya parseado.
        ''' <para>Sin esto el array cae a "hasta agotar" y se come los bytes del vecino: en los
        ''' fragmentos de script de una escena, los <c>Fragments</c> invaden las
        ''' <c>Phase Fragments</c> y el struct se corta en <c>ScriptName</c>.</para></summary>
        Public ReadOnly Property Counter As WbCounter

        Public Sub New(name As String, element As WbValueDef, Optional count As Integer = 0,
                       Optional countPath As String = Nothing, Optional elementNames As String() = Nothing,
                       Optional counter As WbCounter = Nothing)
            Me.Name = name
            _Element = element
            _Count = count
            _CountPath = countPath
            _ElementNames = elementNames
            _Counter = counter
            If element IsNot Nothing Then element.DefParent = Me
        End Sub

        ''' <summary>Bytes del prefijo de conteo (0 si el array no lleva prefijo).</summary>
        Public ReadOnly Property PrefixWidth As Integer
            Get
                Select Case Count
                    Case -1 : Return 4
                    Case -2 : Return 2
                    Case -4 : Return 1
                    Case Else : Return 0
                End Select
            End Get
        End Property

        Public Overrides Function DefaultSize(ctx As WbContext) As Integer
            If Count <= 0 Then Return -1
            Dim es = Element.DefaultSize(ctx)
            If es < 0 Then Return -1
            Return es * Count
        End Function

        ''' <summary>La cantidad la manda otro nodo. La búsqueda SUBE por los ancestros porque un
        ''' contador declarado por firma — el <c>KSIZ</c> de los keywords, por ejemplo — apunta a un
        ''' HERMANO del array dentro del struct que los contiene, y quedarse en el nodo no lo
        ''' encontraría.</summary>
        Private Function ResolveCount(node As WbNode) As Integer
            ' El callback tiene precedencia: convive con una cantidad declarada en 0.
            If Counter IsNot Nothing Then Return Counter(node)
            If Count > 0 Then Return Count
            If String.IsNullOrEmpty(CountPath) Then Return -1
            Dim cn = WbPath.ResolveUpwards(node, CountPath)
            ' Contador declarado pero AUSENTE ⇒ cero elementos, no "hasta agotar". Caso real: el
            ' array 'Materials' del MODT cuelga del cuarto contador del bloque, y ese bloque puede
            ' traer menos de cuatro. Caer a "hasta agotar" se comería los bytes del vecino y el
            ' error se vería recién como un round-trip roto.
            If cn Is Nothing Then Return 0
            Return CInt(Convert.ToInt64(cn.Value))
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Dim pos = offset
            Dim left = avail
            Dim want As Integer

            Dim pw = PrefixWidth
            If pw > 0 AndAlso left <= 0 Then
                ' Subrecord VACÍO con un array de prefijo: 0 elementos y 0 bytes, no un fallo.
                ' Caso real: el NAM5 de cada Body Part de BPTD llega con 0 bytes; tirar acá mata el
                ' array ENTERO de Body Parts y con él el record.
                n.SourceLength = 0
                ' Se marca para que la EMISIÓN tampoco escriba el prefijo: la fuente traía el
                ' subrecord con tamaño 0 y tiene que volver a salir con tamaño 0.
                n.ShortRead = True
                Return n
            End If
            If pw > 0 Then
                Need(n, left, pw)
                Select Case pw
                    Case 4 : want = CInt(BitConverter.ToUInt32(data, pos))
                    Case 2 : want = CInt(BitConverter.ToUInt16(data, pos))
                    Case Else : want = CInt(data(pos))
                End Select
                pos += pw
                left -= pw
            Else
                want = ResolveCount(n)
            End If

            Dim names = ElementNames
            If want >= 0 Then
                For i = 0 To want - 1
                    Dim child = Element.Parse(ctx, data, pos, left, n)
                    If names IsNot Nothing AndAlso i < names.Length Then child.OverrideName = names(i)
                    n.AddChild(child)
                    pos += child.SourceLength
                    left -= child.SourceLength
                Next
            Else
                Dim i = 0
                While left > 0
                    Dim child = Element.Parse(ctx, data, pos, left, n)
                    If child.SourceLength <= 0 Then
                        Throw New WbLayoutException(n.Path, "elemento de array de tamaño 0: el array no podría terminar")
                    End If
                    If names IsNot Nothing AndAlso i < names.Length Then child.OverrideName = names(i)
                    n.AddChild(child)
                    pos += child.SourceLength
                    left -= child.SourceLength
                    i += 1
                End While
            End If
            n.SourceLength = pos - offset
            n.ParsedCount = n.Children.Count
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            ' El contador se DERIVA del array, no al revés. Vale para las dos formas de contador —
            ' el prefijo inline y el nodo remoto al que apunta una ruta —, así que un KSIZ no puede
            ' quedar desincronizado de su KWDA.
            ' Sólo si el array CAMBIÓ respecto de lo parseado (ver WbNode.ParsedCount).
            If Not String.IsNullOrEmpty(CountPath) AndAlso node.ParsedCount <> node.Children.Count Then
                Dim cn = WbPath.ResolveUpwards(node, CountPath)
                If cn IsNot Nothing Then cn.Value = WbCajas.Caja(CLng(node.ChildCount))
            End If
            ' Si la fuente no traía ni el prefijo (subrecord de 0 bytes), tampoco se emite:
            ' escribirlo convertiría un NAM5 de 0 bytes en uno de 4 en cada Body Part de BPTD.
            If Not (node.ShortRead AndAlso node.Children.Count = 0) Then
                Select Case PrefixWidth
                    Case 4 : bw.Write(CUInt(node.Children.Count))
                    Case 2 : bw.Write(CUShort(node.Children.Count))
                    Case 1 : bw.Write(CByte(node.Children.Count))
                End Select
            End If
            For Each c In node.Children
                CType(c.Def, WbValueDef).Emit(c, bw, ctx)
            Next
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            ' Sólo el array de cantidad FIJA nace con elementos. Con prefijo, con contador remoto
            ' o "hasta agotar", un record nuevo arranca con el array vacío y el contador en 0.
            If Count > 0 Then
                For i = 1 To Count
                    Dim c = Element.CreateDefault(ctx)
                    If ElementNames IsNot Nothing AndAlso i - 1 < ElementNames.Length Then c.OverrideName = ElementNames(i - 1)
                    n.AddChild(c)
                Next
            End If
            Return n
        End Function
    End Class

    ''' <summary>Unión de valores: el decisor devuelve el ÍNDICE del miembro que aplica.
    ''' <para>Un campo que sólo existe a partir de cierta Form Version es exactamente esto: una
    ''' unión de dos ramas — "cero bytes" y el campo — decidida por la versión del record. Por eso
    ''' "tamaño = f(versión)" se EXPRESA en la declaración y no hace falta un paso fijo escrito a
    ''' mano: es el caso de <c>DAMA</c>, que pasa de 8 a 12 bytes desde la versión 152.</para></summary>
    Public NotInheritable Class WbUnionDef
        Inherits WbValueDef

        Public ReadOnly Property Decider As WbDecider
        Public ReadOnly Property Members As WbValueDef()

        Public Sub New(name As String, decider As WbDecider, members As WbValueDef())
            Me.Name = name
            _Decider = decider
            _Members = members
            For Each m In members
                If m IsNot Nothing Then m.DefParent = Me
            Next
        End Sub

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim n = NewNode()
            n.Parent = parent
            Dim idx = Decider(ctx, data, offset, avail, parent)
            If idx < 0 OrElse idx >= Members.Length Then
                Throw New WbLayoutException(n.Path, $"el decisor de la unión devolvió {idx}, fuera de [0,{Members.Length - 1}]")
            End If
            n.UnionBranch = idx
            Dim child = Members(idx).Parse(ctx, data, offset, avail, n)
            n.AddChild(child)
            n.SourceLength = child.SourceLength
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            If node.Children.Count = 0 Then Return
            Dim c = node.Children(0)
            CType(c.Def, WbValueDef).Emit(c, bw, ctx)
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim n = NewNode()
            Dim idx = Decider(ctx, Nothing, 0, 0, Nothing)
            If idx < 0 OrElse idx >= Members.Length Then idx = 0
            n.UnionBranch = idx
            n.AddChild(Members(idx).CreateDefault(ctx))
            Return n
        End Function
    End Class

End Namespace
