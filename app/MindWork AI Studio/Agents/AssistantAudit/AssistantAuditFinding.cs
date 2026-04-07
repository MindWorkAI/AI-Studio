using System.Text.Json.Serialization;

namespace AIStudio.Agents.AssistantAudit;

/// <summary>
/// Represents a single structured security finding produced by the assistant audit agent.
/// </summary>
public sealed class AssistantAuditFinding
{
    #pragma warning disable MWAIS0005
    /// <summary>
    /// Gets the normalized internal severity level derived from <see cref="SeverityText"/>.
    /// </summary>
    #pragma warning restore MWAIS0005
    [JsonIgnore]
    public AssistantAuditLevel Severity { get; private init; } = AssistantAuditLevel.UNKNOWN;


    /// <summary>
    /// Gets or initializes the JSON-facing severity label used by the audit model response.
    /// </summary>
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
        
        init => this.Severity = value?.Trim().ToLowerInvariant() switch
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
