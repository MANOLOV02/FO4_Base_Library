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
''' ofrece.</para></summary>
Public NotInheritable Class RecuperacionDeLotes

    Private Sub New()
    End Sub

    ''' <summary>La carpeta de diarios de esta app. Se llama UNA vez al arrancar, antes de cualquier
    ''' guardado: un lote que empiece antes de esto no escribe diario.</summary>
    Public Shared Sub ConfigurarCarpeta(nombreApp As String)
        Try
            Dim raiz = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Dim carpeta = Path.Combine(raiz, nombreApp, "diarios")
            Directory.CreateDirectory(carpeta)
            BSA_BA2_Library_DLL.EscrituraEnElLugar.CarpetaDeDiarios = carpeta
        Catch
            ' Sin carpeta no hay diario: el rollback EN PROCESO sigue igual y la app arranca. No se le
            ' niega el programa al usuario por no poder crear un directorio de metadata.
        End Try
    End Sub

    ''' <summary>Ofrece la recuperación de los lotes interrumpidos. Devuelve las rutas de los proyectos
    ''' afectados, para que el llamador NO los abra automáticamente hasta que el usuario decida.</summary>
    Public Shared Function OfrecerRecuperacion() As List(Of String)
        Dim afectados As New List(Of String)
        Dim pendientes = BSA_BA2_Library_DLL.EscrituraEnElLugar.DiariosPendientes()
        If pendientes.Count = 0 Then Return afectados

        For Each d In pendientes
            Dim entradas = d.Entradas
            If entradas Is Nothing OrElse entradas.Count = 0 Then
                ' Diario vacío: el lote murió antes de su primera operación, así que no tocó nada.
                d.Cerrar()
                Continue For
            End If

            Dim sb As New Text.StringBuilder()
            sb.AppendLine("A previous save did not finish. These files were being written together:")
            sb.AppendLine()
            For Each e In entradas
                sb.AppendLine("  • " & Path.GetFileName(e.Destino) & "   [" & e.Operacion & "]")
                afectados.Add(e.Destino)
            Next
            sb.AppendLine()
            sb.AppendLine("Restore the previous version of these files, or keep what is on disk now?")
            sb.AppendLine()
            sb.AppendLine("Yes = restore the previous version.")
            sb.AppendLine("No  = keep the current files (the backups are left next to them).")

            Dim r = MessageBox.Show(sb.ToString(), "Unfinished save detected",
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If r = DialogResult.Yes Then
                ' ⛔ El diario se borra SOLO si la restauración se completó Y se verificó. Si algo no volvió,
                ' el diario SE QUEDA: el usuario tiene que poder volver a intentarlo en el arranque
                ' siguiente, y sus copias siguen siendo el único ejemplar de lo anterior.
                If RestaurarYVerificar(d) Then d.Cerrar()
            Else
                ' Conservar lo actual: el diario ya no describe nada pendiente. Las copias NO se borran —
                ' son del usuario y las nombra `CopiasPendientes`.
                d.Cerrar()
            End If
        Next
        Return afectados
    End Function

    ''' <summary>Devuelve cada archivo a su copia y COMPRUEBA que quedó. True sólo si volvieron TODOS.
    ''' <para>Se usa la misma primitiva que el rollback en proceso —<c>VolcarEncima</c>, que escribe en el
    ''' lugar y aguanta destinos ocultos— y para un BORRADO se reponen además los atributos anotados.</para>
    ''' <para>⛔ Se intentan TODOS antes de decidir: cortar en el primero dejaría medio lote restaurado, que
    ''' es el estado mezclado que todo esto existe para evitar.</para></summary>
    Private Shared Function RestaurarYVerificar(d As BSA_BA2_Library_DLL.DiarioDeEscritura) As Boolean
        Dim todos As Boolean = True
        Dim fallidos As New List(Of String)
        For Each e In d.Entradas
            Try
                If e.Operacion = "crear" Then
                    ' Deshacer una creación es que el archivo no esté.
                    If File.Exists(e.Destino) Then File.Delete(e.Destino)
                    Continue For
                End If
                If String.IsNullOrEmpty(e.Copia) OrElse Not File.Exists(e.Copia) Then
                    todos = False
                    fallidos.Add(Path.GetFileName(e.Destino) & " (its backup is gone)")
                    Continue For
                End If
                BSA_BA2_Library_DLL.EscrituraEnElLugar.VolcarEncima(e.Copia, e.Destino,
                                                                    permitirOrigenVacio:=True)
                If e.Operacion = "borrar" Then
                    Try
                        File.SetAttributes(e.Destino, CType(e.Atributos, FileAttributes))
                    Catch
                        fallidos.Add(Path.GetFileName(e.Destino) & " (bytes restored, attributes not)")
                    End Try
                End If
            Catch ex As Exception
                todos = False
                fallidos.Add(Path.GetFileName(e.Destino) & ": " & ex.Message)
            End Try
        Next

        If fallidos.Count > 0 Then
            MessageBox.Show("Some files could not be fully restored:" & Environment.NewLine &
                            Environment.NewLine & String.Join(Environment.NewLine, fallidos) &
                            Environment.NewLine & Environment.NewLine &
                            "Their backups were left in place so you can recover them by hand.",
                            "Restore incomplete", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
        End If
        Return todos
    End Function
End Class
