using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The contracts with which System.Text.Json writes the preliminary results the moderator of a SWOT analysis gets,
/// built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Dictionary<string, SwotModerationCategoryInput>))]
internal sealed partial class SwotModerationJsonContext : JsonSerializerContext;