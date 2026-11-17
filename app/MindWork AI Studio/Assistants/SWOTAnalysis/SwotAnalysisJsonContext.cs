using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The contracts with which System.Text.Json reads and writes a SWOT analysis which a chat stores, built at
/// compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwotAnalysisResult))]
internal sealed partial class SwotAnalysisJsonContext : JsonSerializerContext;