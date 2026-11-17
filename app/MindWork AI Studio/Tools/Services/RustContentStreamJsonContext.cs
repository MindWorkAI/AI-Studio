using System.Text.Json.Serialization;

namespace AIStudio.Tools.Services;

/// <summary>
/// The contracts with which System.Text.Json reads the events of a file extraction from the runtime, built at
/// compile time.
/// </summary>
/// <remarks>
/// A converter reads the metadata of an event as the type its JSON tells, so each of these types needs a
/// contract of its own here. A test fails for one which is missing.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ContentStreamSseEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ContentStreamTextMetadata))]
[JsonSerializable(typeof(ContentStreamPdfMetadata))]
[JsonSerializable(typeof(ContentStreamSpreadsheetMetadata))]
[JsonSerializable(typeof(ContentStreamPresentationMetadata))]
[JsonSerializable(typeof(ContentStreamImageMetadata))]
[JsonSerializable(typeof(ContentStreamDocumentMetadata))]
[JsonSerializable(typeof(ContentStreamErrorMetadata))]
[JsonSerializable(typeof(ContentStreamPromptInjectionMetadata))]
internal sealed partial class RustContentStreamJsonContext : JsonSerializerContext;