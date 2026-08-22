# Lector y escritor de records, por campo y por juego

Este directorio contiene un motor que lee y escribe los records de un plugin (`.esp`, `.esm`,
`.esl`) campo por campo, con una única declaración por cada par *(tipo de record, juego)* de la que
se derivan **las dos direcciones**: leer y escribir.

---

## 1. Qué problema resuelve

La estructura de un record —qué campos lleva, en qué orden, de qué tamaño es cada uno, cuáles son
referencias a otros records— estaba escrita a mano en varios lugares que podían desincronizarse:

- el lector, como un `Select Case` plano sobre la firma de cada subrecord;
- el escritor, como una secuencia de llamadas con el orden cableado — y con **tres** secuencias
  distintas para el mismo record según el modo (sobrescribir en Fallout 4, sobrescribir en Skyrim,
  crear nuevo en Skyrim);
- las clases de datos y los borradores de edición, que repetían los mismos campos.

Cambiar uno y no los otros produce exactamente la clase de defecto más cara de este dominio: un
campo numérico leído como referencia a otro archivo, o al revés.

Acá hay **una sola declaración**. El lector la recorre para construir el árbol; el escritor recorre
el árbol para producir los bytes. No hay una segunda transcripción que pueda quedar desfasada.

---

## 2. Cómo está organizado

| Archivo | Qué contiene |
|---|---|
| `WbCore.vb` | El nodo del árbol y el contexto de lectura/escritura |
| `WbValueDefs.vb` | Tipos de campo dentro de un subrecord: entero, decimal, texto, referencia, estructura, arreglo, alternativa |
| `WbMemberDefs.vb` | Nivel de subrecord: subrecord suelto, grupo, arreglo de grupos, alternativa de grupos, y el record entero |
| `WbReader.vb` | Recorre los subrecords del archivo y arma el árbol |
| `WbWriter.vb` | Recorre el árbol y produce los bytes |
| `WbEdit.vb` | Buscar, leer, modificar, agregar y quitar campos del árbol |
| `WbDsl.vb` | Las funciones cortas con las que se escribe una declaración |
| `WbCommon.vb` | Bloques que se repiten en muchos records (modelo, palabras clave, destrucción, plantilla de objeto) |
| `WbDecidersImpl*.vb` | Reglas que eligen entre variantes de un campo según otro campo del mismo record |
| `WbSchema.vb` | Punto de entrada: dado *(juego, firma)* devuelve la declaración |
| `Generated/` | **Generado. No editar a mano.** Las declaraciones de los 137 records de Fallout 4 y los 124 de Skyrim, más las tablas de funciones de condición |

Para regenerar: `python Tools/CanonLayoutGen/emit.py`

---

## 3. Las tres ideas que lo hacen funcionar

### 3.1 Un record es un árbol, no una lista

El formato tiene **dos niveles anidados**:

- **Nivel de subrecord**: la secuencia de bloques con firma de 4 letras que forma el record. Acá
  viven los grupos (varios subrecords que forman una unidad), los arreglos de grupos y las
  alternativas.
- **Nivel de valor**: la estructura interna de los bytes de *un* subrecord. Acá viven los enteros,
  los decimales, los textos, las referencias, las estructuras y los arreglos con contador.

Tratar el record como una lista plana obliga a inventar heurísticas para los subrecords que
aparecen más de una vez con significados distintos. En un árbol el problema no existe: cada
aparición cuelga de un padre diferente y se distingue por su posición.

### 3.2 El recorrido usa dos cursores y el de estructura sólo avanza

Al leer hay un cursor sobre los subrecords del archivo y otro sobre los campos declarados. El
segundo **nunca retrocede**: si el campo declarado actual no puede hacerse cargo del subrecord que
toca, se avanza el cursor de campos y se reintenta con el *mismo* subrecord.

De ahí salen dos consecuencias que hay que tener presentes:

- un grupo **cierra** en cuanto aparece una firma que no le pertenece;
- un arreglo **termina** en cuanto su elemento no puede hacerse cargo del siguiente subrecord.

Un grupo puede marcarse como *desordenado*, y entonces acepta cualquiera de sus firmas y además
reinicia su cursor interno. Es lo que permite que ciertos bloques (el modelo con sus texturas, por
ejemplo) toleren que sus partes vengan en cualquier orden.

### 3.3 Los campos obligatorios sólo importan al crear

Marcar un campo como obligatorio **no** hace que aparezca al leer un record que no lo trae: sólo
interviene cuando se crea un record desde cero.

Eso simplifica mucho la escritura: si el árbol viene de leer un archivo, escribirlo reproduce
exactamente los campos que ese archivo traía. La presencia deja de ser una decisión por cada sitio
que emite y pasa a ser una consecuencia de lo que se leyó.

---

## 4. Lo que el escritor NO hace

El escritor produce bytes **únicamente a partir de nodos del árbol**. No hay ninguna vía por la que
un byte llegue al archivo sin estar representado en el modelo.

Concretamente:

- **Bytes que ningún campo describe.** Si la declaración de un subrecord no cubre todos sus bytes,
  el sobrante entra al árbol como un nodo propio, con nombre (`Bytes sin describir`) y ruta. Se
  puede ver, contar y editar. No es un depósito oculto.
- **Subrecords que la estructura no pudo ubicar.** Escribir uno **lanza una excepción**. Sus bytes
  podrían contener referencias a otros archivos, y copiarlos sin interpretarlos significa que esas
  referencias no pasan por el reindexado de masters: el archivo saldría apuntando al mod
  equivocado, sin aviso. Sólo un arnés de medición puede habilitarlo, y sólo para medir.
- **Contadores.** Un arreglo cuyo tamaño vive en otro campo recalcula ese campo antes de emitir un
  solo byte, de adentro hacia afuera. Si no, un contador que se escribe *antes* que su arreglo sale
  con el valor viejo y el archivo queda corrupto.

### Por qué las referencias no se pueden confundir con números

El reindexado de masters recorre el árbol y toca **exclusivamente** los nodos de tipo referencia. Un
entero de 32 bits que representa un índice de tabla no puede entrar a ese recorrido aunque mida lo
mismo, porque su tipo es otro. No es una convención de nombres ni una lista de excepciones: es la
forma del árbol.

---

## 5. Cómo se verifica

Hay **dos criterios distintos**, y hacen falta los dos.

### Ida y vuelta

Leer un record del archivo, escribirlo de nuevo y comparar byte por byte contra el original.

### Cobertura

Que los campos declarados expliquen **todos** los bytes del record: sin huecos, sin solapes, sin
bloques sin interpretar.

### Por qué no alcanza con el primero

Porque conservar fielmente los bytes que no se entienden hace que la ida y vuelta cierre igual. Un
escritor lo bastante fiel **tapa** una declaración equivocada.

La prueba es directa: si se introduce a propósito un byte de relleno de más en un campo de `ARMA`,
la cobertura salta de 42 a 2.218 avisos y nombra el campo exacto, mientras que la ida y vuelta sigue
dando 100 %. **El criterio de aceptación son los dos juntos.**

---

## 6. Estado medido

Sobre los plugins reales instalados: 71 de Fallout 4, 102 de Skyrim SE. Todos los records de cada
uno. Se reproduce con `Tools/CanonLayoutProbe --edit`.

⚠️ El corpus es la carpeta `Data\` viva del usuario, así que **los totales absolutos se mueven
cuando cambia un mod**. Lo que no se mueve es el criterio: 100 % de ida y vuelta y cada aviso
explicado. Medición del 2026-08-22 20:00 (dos corridas seguidas, idénticas):

| | Records | Ida y vuelta byte a byte | Avisos de cobertura |
|---|---:|---|---:|
| Fallout 4 | 420.728 | **420.728 / 420.728** | 7 |
| Skyrim SE | 331.401 | **331.401 / 331.401** | 421 |
| **Total** | **752.129** | **752.129 / 752.129** | **428** |

- **137 de 137** tipos de record de Fallout 4 y **124 de 124** de Skyrim, **todos con todos sus
  campos traducidos**. Ninguno queda marcado como incompleto. De esos, el corpus instalado ejercita
  132 y 115 respectivamente; el resto no aparece en ningún plugin y por lo tanto su declaración
  está traducida pero no verificada contra datos reales.
- **Cero** subrecords copiados sin interpretar.
- **Cero** textos que no vuelvan a los mismos bytes.
- **86 verificaciones de edición**, todas en verde: borrar un campo, agregarlo en su posición
  correcta, modificarlo, y que los contadores se ajusten solos.

### Los 428 avisos, uno por uno

Ninguno queda sin explicar:

| Caso | Cant. | Por qué |
|---|---:|---|
| `CELL` — indicadores | 331 | El campo se declara de 2 bytes a propósito aunque a veces el archivo trae 1. Es una decisión deliberada del formato para que el mismo campo sirva en varios contextos. |
| `RACE` — nombres de tipo de movimiento | 90 | El campo se declara como texto de 4 caracteres **sin terminador**, así que consume 4. El quinto byte que trae el archivo no lo describe ningún campo. |
| `COBJ` | 3 | `NAM1`, `NAM2` y `NAM3` se declaran **vacíos** —`wbUnused(FIRMA)` es literalmente `wbEmpty`— y el archivo trae 4 bytes en cada uno. El formato los marca además como «no reportar»; nuestro criterio de cobertura es más estricto a propósito y los cuenta. |
| `MATO` | 2 | El record declara versión de formato 25 y 26, pero trae los bytes de un campo que sólo existe desde la 31. |
| `DLVW` | 2 | Dos campos se declaran vacíos: consumen 0 bytes por definición, y el archivo trae 1 y 4. |

En resumen: **los 428 son archivos que se apartan de la definición del formato**, no errores del
lector. Están reportados con su firma y su campo, no tapados.

Hasta el 2026-08-22 a las 19:43 había además 38 avisos de `INNR`: un plugin generado por un
ordenador de inventario escribía la lista de palabras clave **sin su contador delante**, y el grupo
que las contiene sólo puede engancharse por su primer campo, que es justamente el contador.
Desaparecieron cuando ese plugin se regeneró. Es el ejemplo de por qué los totales absolutos son
del corpus y no del motor.

El aviso de `LGTM` que hubo hasta la versión 4.1.6 de diciembre de 2025 —un relleno declarado de 32
bytes del que un record concreto traía 24— **desapareció**: el formato pasó a describir esos 32
bytes campo por campo en vez de dejarlos como relleno.

---

## 7. Estado de integración

⛔ **Esta sección decía, hasta el 2026-08-22, que el motor "no lo llama nadie todavía" y que el lector
y el escritor viejos seguían corriendo. Era FALSO**, y así llegó a manos de un auditor externo, que lo
listó como hallazgo. Un documento de estado que no se mueve con el código induce a error a quien viene
a revisar — que es exactamente para quien está escrito.

**El motor ES el camino en producción.** Lo consumen `RecordParsers`, NPC Manager y
`SaveNpcEspWriter.SerializarRecord`; los parsers y emisores manuales que describía el párrafo viejo
están **borrados** (`RecordDispatcher`, `NpcSubrecordWriter`, `NpcVmadScanner` y las clases `*Record`).

Lo que sigue fuera del motor, a propósito: la cabecera `TES4` y la organización física de los `GRUP`,
que `SaveNpcEspWriter` escribe con código explícito.
