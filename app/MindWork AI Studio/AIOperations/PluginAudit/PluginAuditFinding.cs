using System.Text.Json.Serialization;

namespace AIStudio.AIOperations.PluginAudit;

/// <summary>
/// Represents a single structured security finding produced by the assistant audit agent.
/// </summary>
public sealed class PluginAuditFinding
{
    #pragma warning disable MWAIS0005
    /// <summary>
    /// Gets the normalized internal severity level derived from <see cref="SeverityText"/>.
    /// </summary>
    #pragma warning restore MWAIS0005
    [JsonIgnore]
    public PluginAuditLevel Severity { get; private set; } = PluginAuditLevel.UNKNOWN;


    /// <summary>
    /// Gets or sets the JSON-facing severity label used by the audit model response.
    /// </summary>
    [JsonPropertyName("severity")]
    public string SeverityText
    {
        get => this.Severity switch
        {
            PluginAuditLevel.DANGEROUS => "critical",
            PluginAuditLevel.CAUTION => "medium",
            PluginAuditLevel.SAFE => "low",
            _ => "unknown",
        };
        
        set => this.Severity = value.Trim().ToLowerInvariant() switch
        {
            "critical" => PluginAuditLevel.DANGEROUS,
            "medium" => PluginAuditLevel.CAUTION,
            "low" => PluginAuditLevel.SAFE,
            _ => PluginAuditLevel.UNKNOWN,
        };
    }

    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}