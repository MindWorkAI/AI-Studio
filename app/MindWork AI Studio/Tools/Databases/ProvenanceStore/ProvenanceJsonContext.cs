using System.Text.Json.Serialization;

namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the content around a passage, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ProvenanceJsonContext : JsonSerializerContext;