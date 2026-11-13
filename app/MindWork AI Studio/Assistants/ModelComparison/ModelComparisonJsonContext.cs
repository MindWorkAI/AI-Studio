using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the JSON of the Model Comparison assistant, built at
/// compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ModelComparisonJudgeAnswer))]
internal sealed partial class ModelComparisonJsonContext : JsonSerializerContext;