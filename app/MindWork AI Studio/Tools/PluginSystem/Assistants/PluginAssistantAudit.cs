using AIStudio.Agents.AssistantAudit;

namespace AIStudio.Tools.PluginSystem.Assistants;

public sealed class PluginAssistantAudit
{
    public Guid PluginId { get; set; }
    public string PluginHash { get; set; } = string.Empty;
    public DateTimeOffset AuditedAtUtc { get; set; }
    public string AuditProviderId { get; set; } = string.Empty;
    public string AuditProviderName { get; set; } = string.Empty;
    public AssistantAuditLevel Level { get; set; } = AssistantAuditLevel.UNKNOWN;
    public string Summary { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string PromptPreview { get; set; } = string.Empty;
    public List<AssistantAuditFinding> Findings { get; set; } = [];
}
