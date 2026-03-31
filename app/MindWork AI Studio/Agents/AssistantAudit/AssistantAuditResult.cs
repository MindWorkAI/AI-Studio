namespace AIStudio.Agents.AssistantAudit;

/// <summary>
/// Represents the normalized result returned by the assistant plugin security audit flow.
/// </summary>
public sealed class AssistantAuditResult
{
    /// <summary>
    /// Gets the serialized audit level returned by the model before callers normalize it to <see cref="AssistantAuditLevel"/>.
    /// </summary>
    public string Level { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public float Confidence { get; init; }
    public List<AssistantAuditFinding> Findings { get; init; } = [];
}
