namespace AIStudio.AIOperations.PluginAudit;

/// <summary>
/// Defines the normalized outcome levels used for assistant plugin security audits.
/// </summary>
public enum PluginAuditLevel
{
    UNKNOWN = 0,
    DANGEROUS = 100,
    CAUTION = 200,
    SAFE = 300,
}