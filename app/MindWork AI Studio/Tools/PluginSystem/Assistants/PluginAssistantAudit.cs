using AIStudio.AIOperations.PluginAudit;

namespace AIStudio.Tools.PluginSystem.Assistants;

public sealed class PluginAssistantAudit
{
    public Guid PluginId { get; set; }
    public string PluginHash { get; set; } = string.Empty;
    public DateTimeOffset AuditedAtUtc { get; set; }
    public string AuditProviderId { get; set; } = string.Empty;
    public string AuditProviderName { get; set; } = string.Empty;
    public PluginAuditLevel Level { get; set; } = PluginAuditLevel.UNKNOWN;
    public string Summary { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string PromptPreview { get; set; } = string.Empty;
    public List<PluginAuditFinding> Findings { get; set; } = [];
}
