using System.Text.Json.Serialization;

namespace AIStudio.Agents.AssistantAudit;

public sealed class AssistantAuditFinding
{
    private readonly AssistantAuditLevel severity = AssistantAuditLevel.UNKNOWN;

    [JsonIgnore]
    public AssistantAuditLevel Severity => this.severity;

    [JsonPropertyName("severity")]
    public string SeverityText
    {
        get => this.Severity switch
        {
            AssistantAuditLevel.DANGEROUS => "critical",
            AssistantAuditLevel.CAUTION => "medium",
            AssistantAuditLevel.SAFE => "low",
            _ => "unknown",
        };
        init => this.severity = value?.Trim().ToLowerInvariant() switch
        {
            "critical" => AssistantAuditLevel.DANGEROUS,
            "medium" => AssistantAuditLevel.CAUTION,
            "low" => AssistantAuditLevel.SAFE,
            _ => AssistantAuditLevel.UNKNOWN,
        };
    }

    public string Category { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
