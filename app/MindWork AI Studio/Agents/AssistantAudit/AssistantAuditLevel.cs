namespace AIStudio.Agents.AssistantAudit;

/// <summary>
/// Defines the normalized outcome levels used for assistant plugin security audits.
/// </summary>
public enum AssistantAuditLevel
{
    UNKNOWN = 0,
    DANGEROUS = 100,
    CAUTION = 200,
    SAFE = 300,
}
