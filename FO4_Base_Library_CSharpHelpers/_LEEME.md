# FO4_Base_Library_CSharpHelpers

⛔ **Este proyecto existe por una sola razón: lo que VB.NET no puede compilar.**

No es «la parte C# de la librería». No es un lugar cómodo para escribir código nuevo, ni para lo que
«queda más lindo en C#», ni para lo que uno sabe escribir más rápido. Es la excepción.

## La regla de admisión

Una función entra acá **sólo** si tiene, escrito al lado, el **error de compilación de VB** que la
justifica — textual, con su código `BCxxxxx`. Sin ese error, la función va en VB.

Por qué tan cerrado: en cuanto se acepta «esto conviene en C#», la librería queda partida en dos
lenguajes por gusto, y a partir de ahí toda ley vive en dos lugares. Eso es exactamente lo que la
regla de «una ley en un solo lugar» no permite.

## Lo que hay adentro

| Archivo | Qué hace | El error de VB que lo justifica |
|---|---|---|
| `JsonPrimerValor.cs` | Parsea UN valor JSON e ignora lo que venga después, como `Json::Reader::parse` de jsoncpp (`json_reader.cpp:104-141`, `strictRoot_` = false en `:27-32`). Necesita `JsonReaderOptions.AllowMultipleValues`, que sólo existe en `Utf8JsonReader`. | `BC30668: 'Utf8JsonReader' está obsoleto: 'Types with embedded references are not supported in this version of your compiler.'` |

## Lo que se probó antes de crear el proyecto

Para `JsonPrimerValor`, medido sobre `{"a":1}basura` con las opciones exactas de la app:

| Camino | Resultado |
|---|---|
| `JsonDocument.Parse` | ❌ `'b' is invalid after a single JSON value` |
| `JsonNode.Parse` | ❌ mismo error |
| `JsonSerializer.Deserialize(Of JsonNode)` | ❌ mismo error |
| `DeserializeAsyncEnumerable(topLevelValues:=True)` | ❌ lee por delante y falla |
| `Utf8JsonReader` (sobrecarga `ref reader`) | ✅ pero VB no compila |

`JsonDocumentOptions` no tiene la perilla: sus cuatro propiedades son `CommentHandling`, `MaxDepth`,
`AllowTrailingCommas` y `AllowDuplicateProperties`.

⛔ Ojo con `JsonReaderOptions.AllowMultipleValues`: **no existe en net8.0**, llegó en .NET 9
(`CS0117`). No hace falta — quien exige fin de archivo es `JsonNode.Parse(bytes)`, no el lector;
la sobrecarga que toma el `Utf8JsonReader` para sola al terminar el primer valor. Medido en
**.NET 8.0.30**: `{"a":1}basura` → `{"a":1}`, `BytesConsumed` = 7 de 13.

## Convenciones

- **Mismo repositorio que `FO4_Base_Library`**, a propósito: se versiona, se mueve y se publica con
  ella. No es un tercero, así que declara la configuración `Publish` e importa
  `ConfiguracionPublish.targets` en vez de mapearse a `Release` como `MaterialLib` y `NiflySharp`.
- `RootNamespace` = `FO4_Base_Library`, así que desde VB se llama sin prefijo.
- **AnyCPU y sin ningún RID declarado**, igual que `MaterialLib` y `NiflySharp`: es managed puro, así que
  `PlatformTarget` no cambia nada de lo que produce. Y sin plataformas condicionadas no hay
  `RuntimeIdentifier` SINGULAR condicionado por `$(Platform)`, que es la raíz de la trampa NETSDK1047
  documentada en `FO4_Base_Library.vbproj`: no se declara ninguno y la restauración no tiene targets que
  pisar. ⚠️ Una versión anterior de este archivo decía lo contrario — que declaraba `RuntimeIdentifiers`
  (plural) —, y era falso.
- ⛔ **Tiene que estar en los tres `.sln`.** Un proyecto ausente del `.sln` no recibe configuración del
  build de solución — `AssignProjectConfiguration` no lo encuentra en `ProjectConfigurationPlatforms` y el
  SDK cae en su default. Medido con `bin` limpio: en una corrida `Configuration=Publish` compilaba en
  **Debug**, y ese DLL sin optimizar se copiaba al paquete, en verde. Está en `FO4_NPC_Manager`,
  `Nif_Explorer` y `Wardrobe_Manager` con los 18 mapeos, `Publish→Publish`.
