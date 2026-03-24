namespace AIStudio.Agents.AssistantAudit;

public sealed class AssistantAuditFinding
{
    public string Category { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
}
