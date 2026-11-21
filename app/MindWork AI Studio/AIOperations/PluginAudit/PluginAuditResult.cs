namespace AIStudio.AIOperations.PluginAudit;

/// <summary>
/// Represents the normalized result returned by the assistant plugin security audit flow.
/// </summary>
public sealed record PluginAuditResult
{
    /// <summary>
    /// Gets the serialized audit level returned by the model before callers normalize it to <see cref="PluginAuditLevel"/>.
    /// </summary>
    public string Level { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public List<PluginAuditFinding> Findings { get; set; } = [];
}
