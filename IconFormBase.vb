''' <summary>
''' Formulario base del que heredan los formularios de las tres apps para tener los iconos de la UI.
''' Trae dos <see cref="System.Windows.Forms.ImageList"/> compartidos, uno por medida, y NADA MÁS.
''' </summary>
''' <remarks>
''' <para><b>Cómo se usa.</b> En el <c>.Designer.vb</c> del formulario, cambiar una línea:</para>
''' <code>
''' Inherits System.Windows.Forms.Form        →   Inherits FO4_Base_Library.IconFormBase
''' </code>
''' <para>A partir de ahí, en cualquier <c>Button</c>, <c>Label</c> o <c>TabPage</c> se elige
''' <c>ImageList = IconsSmall</c> y después el <c>ImageKey</c> del desplegable, que muestra las
''' miniaturas. Es el camino nativo del diseñador: se ve en el lienzo.</para>
'''
''' <para><b>Qué se gana.</b> Los bytes de los iconos viven UNA sola vez, en el <c>.resx</c> de este
''' formulario, dentro de la librería. Medido en un arnés de dos ensamblados: la app que hereda no
''' lleva ni un byte de imagen (lib.dll 84 KB con los 57 iconos, app.dll 7 KB, cero .resx propio).
''' Antes cada formulario guardaba su propia copia en su resx — tres en Wardrobe Manager, una en
''' Ba2 — así que cambiar un icono había que hacerlo en cada uno.</para>
'''
''' <para>Para agregar o cambiar un icono: se toca el PNG en <c>Resources\Icons\&lt;medida&gt;</c> y se
''' reimporta en el <c>ImageList</c> de ESTE formulario. Con eso cambia en las tres apps. Ver
''' <c>Resources\Icons\_LEEME.txt</c>.</para>
'''
''' <para><b>Este formulario tiene que quedar PELADO.</b> Sin controles, y sin tocar
''' <c>Size</c>, <c>Text</c>, <c>Icon</c>, <c>AutoScaleMode</c> ni <c>StartPosition</c>: todo eso lo
''' heredan los formularios derivados. Con cero controles no hay nada bloqueado ni movido en ellos,
''' que es la razón por la que este patrón no cambia el aspecto de nada.</para>
'''
''' <para><b>Los dos ImageList son <c>Protected</c>, no <c>Friend</c>.</b> El diseñador de VB
''' escribe <c>Friend WithEvents</c> por defecto, y <c>Friend</c> no cruza el límite de ensamblado:
''' desde Wardrobe Manager el campo no existiría. Está anotado también en el
''' <c>.Designer.vb</c>, que es donde el diseñador podría pisarlo.</para>
'''
''' <para><b>El costo.</b> <c>FO4_Base_Library</c> pasa a ser dependencia DE DISEÑO de cada
''' formulario que herede: si la librería no compila, esos formularios no abren en el diseñador.</para>
''' </remarks>
Public Class IconFormBase

End Class
