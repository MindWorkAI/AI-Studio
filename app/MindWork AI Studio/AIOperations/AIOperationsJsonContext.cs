using System.Text.Json.Serialization;

using AIStudio.AIOperations.DataSourceChoice;

namespace AIStudio.AIOperations;

/// <summary>
/// The contracts with which System.Text.Json reads the answers of models to AI operations, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<SelectedDataSource>))]
internal sealed partial class AIOperationsJsonContext : JsonSerializerContext;
