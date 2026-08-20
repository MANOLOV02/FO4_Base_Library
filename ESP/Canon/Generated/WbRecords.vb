' ============================================================================================
' ARCHIVO GENERADO — NO EDITAR A MANO.  Regenerar: Tools/CanonViewGen
'
' Como se abre o se crea cada tipo de record.
' El nombre de cada propiedad ES el nombre del campo en el formato: no hay ninguna
' tabla de equivalencias que mantener, y si el formato cambia un campo el codigo que
' lo usaba deja de compilar.
' ============================================================================================

Namespace Canon

    ''' <summary>Punto de entrada a los records.
    ''' <para>Abrir devuelve la interfaz comun a los dos juegos. Si hace falta un campo
    ''' que solo existe en uno, hay que convertir a la clase de ese juego: es explicito a
    ''' proposito, porque varios campos comparten nombre con significados distintos.</para>
    ''' <para>Crear devuelve un record vacio con los campos que el formato marca como
    ''' obligatorios. Se escribe igual que uno leido, y guardarlo emite lo que se ve.</para></summary>
    Public Module CanonRecords

        ''' <summary>Abre un record ARMA ya existente.</summary>
        Public Function Arma(rec As PluginRecord, plugins As PluginManager) As IArma
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New ArmaFO4(raiz, ctx, res)
            Return New ArmaSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record ARMA nuevo, vacio.</summary>
        Public Function ArmaNuevo(game As WbGame) As IArma
            Dim def = WbSchema.Get(game, "ARMA")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "ARMA"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New ArmaFO4(raiz, ctx, res)
            Return New ArmaSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record ARMO ya existente.</summary>
        Public Function Armo(rec As PluginRecord, plugins As PluginManager) As IArmo
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New ArmoFO4(raiz, ctx, res)
            Return New ArmoSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record ARMO nuevo, vacio.</summary>
        Public Function ArmoNuevo(game As WbGame) As IArmo
            Dim def = WbSchema.Get(game, "ARMO")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "ARMO"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New ArmoFO4(raiz, ctx, res)
            Return New ArmoSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record BPTD ya existente.</summary>
        Public Function Bptd(rec As PluginRecord, plugins As PluginManager) As IBptd
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New BptdFO4(raiz, ctx, res)
            Return New BptdSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record BPTD nuevo, vacio.</summary>
        Public Function BptdNuevo(game As WbGame) As IBptd
            Dim def = WbSchema.Get(game, "BPTD")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "BPTD"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New BptdFO4(raiz, ctx, res)
            Return New BptdSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record CLFM ya existente.</summary>
        Public Function Clfm(rec As PluginRecord, plugins As PluginManager) As IClfm
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New ClfmFO4(raiz, ctx, res)
            Return New ClfmSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record CLFM nuevo, vacio.</summary>
        Public Function ClfmNuevo(game As WbGame) As IClfm
            Dim def = WbSchema.Get(game, "CLFM")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "CLFM"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New ClfmFO4(raiz, ctx, res)
            Return New ClfmSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record DFOB ya existente.</summary>
        Public Function Dfob(rec As PluginRecord, plugins As PluginManager) As IDfob
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            Return New DfobFO4(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record DFOB nuevo, vacio.</summary>
        Public Function DfobNuevo(game As WbGame) As IDfob
            Dim def = WbSchema.Get(game, "DFOB")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "DFOB"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            Return New DfobFO4(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record FLST ya existente.</summary>
        Public Function Flst(rec As PluginRecord, plugins As PluginManager) As IFlst
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New FlstFO4(raiz, ctx, res)
            Return New FlstSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record FLST nuevo, vacio.</summary>
        Public Function FlstNuevo(game As WbGame) As IFlst
            Dim def = WbSchema.Get(game, "FLST")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "FLST"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New FlstFO4(raiz, ctx, res)
            Return New FlstSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record HDPT ya existente.</summary>
        Public Function Hdpt(rec As PluginRecord, plugins As PluginManager) As IHdpt
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New HdptFO4(raiz, ctx, res)
            Return New HdptSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record HDPT nuevo, vacio.</summary>
        Public Function HdptNuevo(game As WbGame) As IHdpt
            Dim def = WbSchema.Get(game, "HDPT")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "HDPT"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New HdptFO4(raiz, ctx, res)
            Return New HdptSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record LVLI ya existente.</summary>
        Public Function Lvli(rec As PluginRecord, plugins As PluginManager) As ILvli
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New LvliFO4(raiz, ctx, res)
            Return New LvliSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record LVLI nuevo, vacio.</summary>
        Public Function LvliNuevo(game As WbGame) As ILvli
            Dim def = WbSchema.Get(game, "LVLI")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "LVLI"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New LvliFO4(raiz, ctx, res)
            Return New LvliSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record LVLN ya existente.</summary>
        Public Function Lvln(rec As PluginRecord, plugins As PluginManager) As ILvln
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New LvlnFO4(raiz, ctx, res)
            Return New LvlnSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record LVLN nuevo, vacio.</summary>
        Public Function LvlnNuevo(game As WbGame) As ILvln
            Dim def = WbSchema.Get(game, "LVLN")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "LVLN"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New LvlnFO4(raiz, ctx, res)
            Return New LvlnSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record MSWP ya existente.</summary>
        Public Function Mswp(rec As PluginRecord, plugins As PluginManager) As IMswp
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            Return New MswpFO4(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record MSWP nuevo, vacio.</summary>
        Public Function MswpNuevo(game As WbGame) As IMswp
            Dim def = WbSchema.Get(game, "MSWP")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "MSWP"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            Return New MswpFO4(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record NPC_ ya existente.</summary>
        Public Function Npc(rec As PluginRecord, plugins As PluginManager) As INpc
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New NpcFO4(raiz, ctx, res)
            Return New NpcSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record NPC_ nuevo, vacio.</summary>
        Public Function NpcNuevo(game As WbGame) As INpc
            Dim def = WbSchema.Get(game, "NPC_")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "NPC_"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New NpcFO4(raiz, ctx, res)
            Return New NpcSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record OMOD ya existente.</summary>
        Public Function Omod(rec As PluginRecord, plugins As PluginManager) As IOmod
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            Return New OmodFO4(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record OMOD nuevo, vacio.</summary>
        Public Function OmodNuevo(game As WbGame) As IOmod
            Dim def = WbSchema.Get(game, "OMOD")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "OMOD"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            Return New OmodFO4(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record OTFT ya existente.</summary>
        Public Function Otft(rec As PluginRecord, plugins As PluginManager) As IOtft
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New OtftFO4(raiz, ctx, res)
            Return New OtftSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record OTFT nuevo, vacio.</summary>
        Public Function OtftNuevo(game As WbGame) As IOtft
            Dim def = WbSchema.Get(game, "OTFT")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "OTFT"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New OtftFO4(raiz, ctx, res)
            Return New OtftSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record RACE ya existente.</summary>
        Public Function Race(rec As PluginRecord, plugins As PluginManager) As IRace
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New RaceFO4(raiz, ctx, res)
            Return New RaceSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record RACE nuevo, vacio.</summary>
        Public Function RaceNuevo(game As WbGame) As IRace
            Dim def = WbSchema.Get(game, "RACE")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "RACE"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New RaceFO4(raiz, ctx, res)
            Return New RaceSSE(raiz, ctx, res)
        End Function

        ''' <summary>Abre un record TXST ya existente.</summary>
        Public Function Txst(rec As PluginRecord, plugins As PluginManager) As ITxst
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Dim res As New CanonResolver(rec, plugins)
            If ctx.Game = WbGame.Fallout4 Then Return New TxstFO4(raiz, ctx, res)
            Return New TxstSSE(raiz, ctx, res)
        End Function

        ''' <summary>Crea un record TXST nuevo, vacio.</summary>
        Public Function TxstNuevo(game As WbGame) As ITxst
            Dim def = WbSchema.Get(game, "TXST")
            If def Is Nothing Then Return Nothing
            Dim ctx As New WbContext(game) With {.RecordSignature = "TXST"}
            Dim raiz = WbReader.CreateNew(def, ctx)
            Dim res As CanonResolver = Nothing
            If ctx.Game = WbGame.Fallout4 Then Return New TxstFO4(raiz, ctx, res)
            Return New TxstSSE(raiz, ctx, res)
        End Function

    End Module

End Namespace
