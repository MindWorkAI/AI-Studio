using AIStudio.Tools.PluginSystem;

namespace AIStudio.Agents.AssistantAudit;

public static class AssistantAuditLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantAuditLevelExtensions).Namespace, nameof(AssistantAuditLevelExtensions));

    public static string GetName(this AssistantAuditLevel level) => level switch
    {
        AssistantAuditLevel.DANGEROUS => TB("Dangerous"),
        AssistantAuditLevel.CAUTION => TB("Needs Review"),
        AssistantAuditLevel.SAFE => TB("Safe"),
        _ => TB("Unknown"),
    };

    public static Severity GetSeverity(this AssistantAuditLevel level) => level switch
    {
        AssistantAuditLevel.DANGEROUS => Severity.Error,
        AssistantAuditLevel.CAUTION => Severity.Warning,
        AssistantAuditLevel.SAFE => Severity.Success,
        _ => Severity.Info,
    };

    public static AssistantAuditLevel Parse(string? value) => Enum.TryParse<AssistantAuditLevel>(value, true, out var level) ? level : AssistantAuditLevel.UNKNOWN;
}
