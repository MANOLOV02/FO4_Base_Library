Imports System.IO
Imports System.Windows.Forms

''' <summary>El arranque de la app: qué hacer con un guardado que quedó a medias porque el proceso se
''' terminó de golpe.
'''
''' <para>⛔ POR QUE VIVE ACA Y NO EN `EscrituraEnElLugar`. Esa clase es librería DISTRIBUIDA y no le habla
''' al usuario — es la misma razón por la que `NifContent_Class` dejó de abrir modales. La librería DETECTA
''' y DESCRIBE (`DiariosPendientes`); la decisión es de la app, y el diálogo vive acá, compartido por
''' NPC Manager y Wardrobe Manager para que no haya dos textos distintos para la misma pregunta.</para>
'''
''' <para>⛔ NO SE RESTAURA SOLO, y no es prudencia decorativa: después de un reinicio nadie puede saber si
''' el usuario quiere volver a la versión anterior o quedarse con lo que alcanzó a escribirse. Decidir por
''' él es pisarle el trabajo en la mitad de los casos. Se muestra qué archivos están afectados y se
''' ofrece.</para>
'''
''' <para>⛔⛔ ESTE ARCHIVO ES EL OTRO EXTREMO DEL PROTOCOLO DE DOS ESTADOS. Lo que hace `Confirmar` y
''' `CerrarRollbackTerminadoSinTirar` dentro del proceso, acá lo hace el arranque siguiente:
''' <c>MarcarResuelto → borrar copias → borrar diario</c>. Un diario que ya está `resuelto` NO se le ofrece
''' al usuario NUNCA — sus destinos ya contienen una decisión completa y lo único que falta es limpiar. El
''' bug que eso cierra es concreto: antes, un corte durante la limpieza de un guardado CONFIRMADO dejaba un
''' diario que el arranque siguiente ofrecía "restaurar", con la mitad de las copias ya borradas — cosecha
''' mixta sobre archivos buenos.</para></summary>
Public NotInheritable Class RecuperacionDeLotes

    Private Sub New()
    End Sub

    ''' <summary>La carpeta de diarios de esta app. Se llama UNA vez al arrancar, antes de cualquier
    ''' guardado: un lote que empiece antes de esto no escribe diario.
    ''' <para>⛔ YA NO TRAGA LA FALLA. Antes tenía un `Catch` vacío que dejaba `CarpetaDeDiarios` en cadena
    ''' vacía, y a partir de ahí la app entera guardaba SIN diario creyendo que lo tenía: exactamente la
    ''' degradación silenciosa que <c>AbrirDiario</c> deja de hacer del otro lado. Si no se puede crear una
    ''' carpeta en <c>LocalApplicationData</c>, el arranque falla con nombre y stack en vez de arrancar
    ''' desprotegido — el crash handler ya está instalado cuando esto corre.</para></summary>
    Public Shared Sub ConfigurarCarpeta(nombreApp As String)
        Dim raiz = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim carpeta = Path.Combine(raiz, nombreApp, "diarios")
        Directory.CreateDirectory(carpeta)
        BSA_BA2_Library_DLL.EscrituraEnElLugar.CarpetaDeDiarios = carpeta
    End Sub

    ''' <summary>Ofrece la recuperación de los lotes interrumpidos, uno por uno.
    ''' <para>⛔ ES `Sub` Y NO DEVUELVE "AFECTADOS". La lista anterior era una promesa que ningún llamador
    ''' cumplía —los dos la descartan—, y peor: sugería que después de esto podía quedar un proyecto en un
    ''' estado ambiguo que la app tenía que evitar abrir. Con el protocolo de dos estados no queda: o el
    ''' usuario decidió y el diario está resuelto, o eligió <i>decidir después</i> y no se tocó nada.</para>
    ''' <para>Las cuatro ramas son las cuatro situaciones reales, y ninguna se puede colapsar:</para>
    ''' <list type="bullet">
    ''' <item><b>ilegible</b> ⇒ se MUESTRA y se DEJA. Un diario que no se pudo interpretar no describe una
    ''' transacción que se pueda deshacer, y borrarlo destruiría la única pista de que hubo un guardado a
    ''' medias — con copias al lado de los archivos del usuario que ya nadie sabría explicar;</item>
    ''' <item><b>resuelto</b> ⇒ se limpia, sin preguntar. Los destinos ya son finales;</item>
    ''' <item><b>vacío</b> ⇒ el lote murió antes de su primera operación: no tocó nada, se resuelve y se
    ''' cierra;</item>
    ''' <item><b>en curso con entradas</b> ⇒ la única que se le pregunta al usuario.</item></list>
    ''' <para>⛔ Y HAY TERCERA OPCION. <i>Cancel</i> no toca diario, ni copias, ni destinos: el usuario que no
    ''' sabe qué contestar puede cerrar el diálogo, mirar sus archivos y volver a arrancar. Con sólo Sí/No,
    ''' la salida por la X del diálogo equivalía a "conservar lo actual" y le resolvía el dilema por
    ''' él.</para></summary>
    Public Shared Sub OfrecerRecuperacion()
        For Each d In BSA_BA2_Library_DLL.EscrituraEnElLugar.DiariosPendientes()
            If Not d.EsLegible Then
                MessageBox.Show("Unreadable recovery journal:" & Environment.NewLine & d.Ruta &
                                Environment.NewLine & d.ErrorDeLectura,
                                "Recovery journal error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Continue For
            End If

            If d.Estado = "resuelto" Then
                LimpiarResuelto(d)
                Continue For
            End If

            If d.Entradas.Count = 0 Then
                d.MarcarResuelto()
                d.Cerrar()
                Continue For
            End If

            Dim sb As New Text.StringBuilder()
            sb.AppendLine("A previous save did not finish. These files were being written together:")
            sb.AppendLine()
            For Each e In d.Entradas
                sb.AppendLine("  • " & Path.GetFileName(e.Destino) & "   [" & e.Operacion & "]")
            Next
            sb.AppendLine()
            sb.AppendLine("Yes = restore the previous state.")
            sb.AppendLine("No = keep the current state.")
            sb.AppendLine("Cancel = decide later (nothing is touched).")

            Select Case MessageBox.Show(sb.ToString(), "Unfinished save",
                                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning)
                Case DialogResult.Yes
                    ' ⛔ `MarcarResuelto` ANTES de borrar una sola copia: si el proceso muere entre medio,
                    ' el arranque siguiente RETOMA LA LIMPIEZA en vez de ofrecer restaurar de nuevo sobre
                    ' archivos que ya fueron restaurados.
                    If RestaurarAnterior(d) Then
                        d.MarcarResuelto()
                        LimpiarResuelto(d)
                    End If
                Case DialogResult.No
                    ' Conservar lo actual TAMBIEN es una decisión completa: los destinos quedan como están
                    ' y las copias dejan de hacer falta.
                    d.MarcarResuelto()
                    LimpiarResuelto(d)
                Case DialogResult.Cancel
                    ' No tocar diario, copias ni destinos. Se vuelve a ofrecer en el próximo arranque.
            End Select
        Next
    End Sub

    ''' <summary>Devuelve cada archivo a su copia y COMPRUEBA que quedó. True sólo si volvieron TODOS.
    ''' <para>⛔ ORDEN INVERSO, igual que el rollback en proceso. Un lote puede tocar el MISMO destino más de
    ''' una vez (escribir y después borrar un sidecar, por ejemplo); recorriendo hacia adelante, la última
    ''' entrada que se aplica es la más NUEVA y el archivo queda en el estado intermedio, no en el original.
    ''' Hacia atrás, la que manda al final es la primera copia tomada — que es la versión que el usuario
    ''' tenía antes de que empezara el guardado.</para>
    ''' <para>Se usa la misma primitiva que el rollback en proceso —<c>VolcarEncima</c>, que escribe en el
    ''' lugar y aguanta destinos ocultos— y para un BORRADO se reponen además los atributos anotados.</para>
    ''' <para>⛔ SE VERIFICA TODO LO QUE SE HACE: los bytes con <c>MismoContenido</c>, la desaparición del
    ''' archivo creado con <c>File.Exists</c>, y los atributos releyéndolos. Sin eso, "restaurado" quería
    ''' decir solamente "no tiró excepción" — y un volcado corto o un `Delete` diferido pasan por ahí sin
    ''' tirar nada, dejando al usuario con un diario borrado, sus copias borradas y el archivo a medias.</para>
    ''' <para>⛔ Se intentan TODOS antes de decidir: cortar en el primero dejaría medio lote restaurado, que
    ''' es el estado mezclado que todo esto existe para evitar. Y si algo falló se devuelve False, con lo
    ''' cual el diario NO pasa a resuelto y NINGUNA copia se borra: el usuario puede reintentarlo en el
    ''' arranque siguiente.</para></summary>
    Private Shared Function RestaurarAnterior(d As BSA_BA2_Library_DLL.DiarioDeEscritura) As Boolean
        Dim errores As New List(Of String)
        For i = d.Entradas.Count - 1 To 0 Step -1
            Dim e = d.Entradas(i)
            Try
                Select Case e.Operacion
                    Case "crear"
                        ' Deshacer una creación es que el archivo NO esté.
                        If File.Exists(e.Destino) Then
                            Try : File.SetAttributes(e.Destino, FileAttributes.Normal) : Catch : End Try
                            File.Delete(e.Destino)
                        End If
                        ' `File.Delete` puede volver sin excepción y dejar el archivo (borrado diferido:
                        ' alguien lo tiene abierto con FILE_SHARE_DELETE).
                        If File.Exists(e.Destino) Then Throw New IOException("File still exists.")

                    Case "reemplazar", "borrar"
                        If Not File.Exists(e.Copia) Then
                            Throw New FileNotFoundException("Backup missing.", e.Copia)
                        End If
                        BSA_BA2_Library_DLL.EscrituraEnElLugar.VolcarEncima(
                            e.Copia, e.Destino, permitirOrigenVacio:=True)
                        If Not BSA_BA2_Library_DLL.EscrituraEnElLugar.MismoContenido(e.Copia, e.Destino) Then
                            Throw New IOException("Restored bytes do not match the backup.")
                        End If
                        If e.Operacion = "borrar" Then
                            ' ⛔ MASCARA, NO VALOR CRUDO — y la MISMA que usó el registro. `GetAttributes`
                            ' devuelve bits que administra el filesystem (`Compressed` en un volumen
                            ' comprimido, `SparseFile`, `ReparsePoint`) que `SetAttributes` no controla:
                            ' comparar contra el valor completo fallaría SIEMPRE y esta recuperación
                            ' declararía roto un archivo que volvió perfecto.
                            Dim atributos = BSA_BA2_Library_DLL.EscrituraEnElLugar.AtributosRestaurables(
                                CType(e.Atributos, FileAttributes))
                            File.SetAttributes(e.Destino, atributos)
                            Dim actuales = BSA_BA2_Library_DLL.EscrituraEnElLugar.AtributosRestaurables(
                                File.GetAttributes(e.Destino))
                            If actuales <> atributos Then
                                Throw New IOException("Original restorable attributes were not restored.")
                            End If
                        End If
                End Select
            Catch ex As Exception
                errores.Add(e.Destino & ": " & ex.Message)
            End Try
        Next

        If errores.Count = 0 Then Return True
        MessageBox.Show("Recovery incomplete:" & Environment.NewLine &
                        String.Join(Environment.NewLine, errores) & Environment.NewLine &
                        "Journal and backups were retained.",
                        "Recovery incomplete", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Return False
    End Function

    ''' <summary>Sólo recibe diarios RESUELTOS. Nunca restaura destinos.
    ''' <para>⛔ ES LA MITAD IRREVERSIBLE DEL PROTOCOLO y por eso el contrato está escrito acá arriba: si
    ''' alguna vez entrara un diario `en_curso`, esto le borraría las copias que son el único ejemplar de lo
    ''' anterior. Sus dos únicos llamadores lo hacen después de ver `Estado = "resuelto"` o de haber llamado
    ''' `MarcarResuelto` con éxito.</para>
    ''' <para>Se deduplica por ruta: dos entradas del mismo lote pueden compartir copia, y borrar dos veces
    ''' la misma ruta haría que la segunda vuelta la contara como sobrante.</para>
    ''' <para>Si queda alguna copia sin borrar, el diario NO se cierra: la limpieza se retoma sola en el
    ''' arranque siguiente — y es inofensivo, porque `resuelto` ya prohíbe volver a ofrecer
    ''' restauración.</para></summary>
    Private Shared Sub LimpiarResuelto(d As BSA_BA2_Library_DLL.DiarioDeEscritura)
        Dim sobrantes As New List(Of String)
        For Each copia In d.Entradas.Select(Function(e) e.Copia).
            Where(Function(ruta) Not String.IsNullOrEmpty(ruta)).
            Distinct(StringComparer.OrdinalIgnoreCase)
            Try
                If File.Exists(copia) Then
                    ' Una copia de SOLO LECTURA no se deja borrar; la copia es nuestra, no del usuario.
                    Try : File.SetAttributes(copia, FileAttributes.Normal) : Catch : End Try
                    File.Delete(copia)
                End If
                If File.Exists(copia) Then sobrantes.Add(copia)
            Catch
                sobrantes.Add(copia)
            End Try
        Next

        If sobrantes.Count = 0 Then
            d.Cerrar()
        Else
            MessageBox.Show("The save decision is complete, but these backups could not be removed:" &
                            Environment.NewLine & String.Join(Environment.NewLine, sobrantes) &
                            Environment.NewLine & "Cleanup will be retried next start.",
                            "Backup cleanup pending", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub
End Class
