using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The contracts with which System.Text.Json reads the answers of the models which work on a SWOT analysis, built at
/// compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwotCategoryAnalysisResult))]
[JsonSerializable(typeof(SwotModerationResult))]
internal sealed partial class SwotModelAnswerJsonContext : JsonSerializerContext;