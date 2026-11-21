using System.Text.Json.Serialization;

using AIStudio.AIOperations.PluginAudit;

namespace AIStudio.Agents;

/// <summary>
/// The contracts with which System.Text.Json reads the answers of the agents, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PluginAuditResult))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;