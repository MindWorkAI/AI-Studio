namespace AIStudio.Agents.AssistantAudit;

public sealed class AssistantAuditResult
{
    public string Level { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public float Confidence { get; init; }
    public List<AssistantAuditFinding> Findings { get; init; } = [];
}
