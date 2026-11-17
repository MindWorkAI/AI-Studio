using System.Text.Json.Serialization;

using AIStudio.Agents.AssistantAudit;

namespace AIStudio.Agents;

/// <summary>
/// The contracts with which System.Text.Json reads the answers of the agents, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<SelectedDataSource>))]
[JsonSerializable(typeof(RetrievalContextValidationResult))]
[JsonSerializable(typeof(AssistantAuditResult))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;