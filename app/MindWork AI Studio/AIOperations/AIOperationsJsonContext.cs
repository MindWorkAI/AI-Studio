using System.Text.Json.Serialization;

using AIStudio.AIOperations.DataSourceChoice;
using AIStudio.AIOperations.PluginAudit;
using AIStudio.AIOperations.RetrievalContextValidation;

namespace AIStudio.AIOperations;

/// <summary>
/// The contracts with which System.Text.Json reads the answers of models to AI operations, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<SelectedDataSource>))]
[JsonSerializable(typeof(RetrievalContextValidationAnswer))]
[JsonSerializable(typeof(PluginAuditResult))]
internal sealed partial class AIOperationsJsonContext : JsonSerializerContext;