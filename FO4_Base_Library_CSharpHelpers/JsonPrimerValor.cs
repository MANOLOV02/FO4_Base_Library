using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FO4_Base_Library
{
    /// <summary>
    /// Parseo de UN valor JSON ignorando lo que venga después, como hace jsoncpp — la ley que rige
    /// los presets de los dos motores.
    ///
    /// <para><b>Qué hace el motor.</b> <c>Json::Reader::parse</c> (jsoncpp json_reader.cpp:104-141)
    /// llama <c>readValue()</c>, saltea comentarios y devuelve: <b>nunca comprueba
    /// <c>current_ == end_</c></b>. El único chequeo posterior es <c>if (features_.strictRoot_)</c>, y
    /// <c>strictRoot_</c> vale <c>false</c> tanto en <c>Features()</c> como en <c>Features::all()</c>
    /// (:27-32). Los dos motores caen ahí: f4ee usa <c>Json::Reader reader;</c> (ctor por defecto =
    /// <c>Features::all()</c>, CharGenInterface.cpp:271) y skee64 pasa <c>features.all()</c> explícito
    /// (PresetInterface.cpp:911-914). O sea: <c>{…}basura</c> el motor LO CARGA.</para>
    ///
    /// <para><b>Por qué está acá y no en VB.</b> <c>JsonDocument.Parse</c>, <c>JsonNode.Parse</c> y
    /// <c>JsonSerializer.Deserialize</c> exigen fin de archivo y tiran
    /// <c>'x' is invalid after a single JSON value. Expected end of data</c>; <c>JsonDocumentOptions</c>
    /// no tiene la perilla (sus cuatro propiedades son CommentHandling, MaxDepth, AllowTrailingCommas y
    /// AllowDuplicateProperties). La perilla es <c>JsonReaderOptions.AllowMultipleValues</c>, que sólo
    /// existe en <c>Utf8JsonReader</c> — y VB.NET no lo compila:
    /// <c>BC30668: 'Utf8JsonReader' está obsoleto: 'Types with embedded references are not supported in
    /// this version of your compiler.'</c> Medido también, y descartado, el camino
    /// <c>DeserializeAsyncEnumerable(topLevelValues: true)</c>: lee por delante y falla igual con la
    /// basura del final.</para>
    ///
    /// <para><b>Lo que NO cambia.</b> Se conservan las dos leyes que ya estaban con cita: los
    /// comentarios se saltean (<c>Features::all()</c> trae <c>allowComments_</c>) y la coma final sigue
    /// siendo error (json_reader.cpp:413-425 en objetos, :468-474 en arreglos). Un JSON incompleto o un
    /// archivo vacío siguen lanzando <see cref="JsonException"/>, que es lo que el llamador ya
    /// atrapaba.</para>
    /// </summary>
    public static class JsonPrimerValor
    {
        /// <summary>Opciones del lector: exactamente las que los dos loaders ya usaban.
        ///
        /// <para>⛔ <c>AllowMultipleValues</c> NO va acá, y no por olvido: <b>no existe en net8.0</b>
        /// (llegó en .NET 9) y el build corta con
        /// <c>CS0117: 'JsonReaderOptions' no contiene una definición para 'AllowMultipleValues'</c>.
        /// Tampoco hace falta: quien exige fin de archivo es <c>JsonNode.Parse(bytes)</c> /
        /// <c>JsonDocument.Parse(bytes)</c>, no el lector. Las sobrecargas que toman el
        /// <c>Utf8JsonReader</c> leen UN valor y devuelven sin mirar lo que sigue.
        /// Medido en .NET 8.0.30 sobre <c>{"a":1}basura</c>: las dos devuelven <c>{"a":1}</c> con
        /// <c>BytesConsumed</c> = 7 de 13. Y siguen fallando la coma final, el JSON incompleto y el
        /// archivo vacío.</para></summary>
        private static readonly JsonReaderOptions Opciones = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };

        /// <summary>Primer valor como <see cref="JsonNode"/>. Lanza <see cref="JsonException"/> igual
        /// que <c>JsonNode.Parse</c> cuando el valor en sí es inválido.</summary>
        public static JsonNode? Nodo(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var lector = new Utf8JsonReader(bytes, Opciones);
            return JsonNode.Parse(ref lector);
        }

        /// <summary>Primer valor como <see cref="JsonDocument"/>. El llamador lo tiene que liberar,
        /// igual que con <c>JsonDocument.Parse</c>.</summary>
        public static JsonDocument Documento(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var lector = new Utf8JsonReader(bytes, Opciones);
            return JsonDocument.ParseValue(ref lector);
        }
    }
}
