using AIStudio.Tools.PluginSystem;

namespace AIStudio.Agents.AssistantAudit;

public static class AssistantAuditLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantAuditLevelExtensions).Namespace, nameof(AssistantAuditLevelExtensions));

    extension(AssistantAuditLevel level)
    {
        public string GetName() => level switch
        {
            AssistantAuditLevel.DANGEROUS => TB("Dangerous"),
            AssistantAuditLevel.CAUTION => TB("Concerning"),
            AssistantAuditLevel.SAFE => TB("Safe"),
            _ => TB("Unknown"),
        };

        public Severity GetSeverity() => level switch
        {
            AssistantAuditLevel.DANGEROUS => Severity.Error,
            AssistantAuditLevel.CAUTION => Severity.Warning,
            AssistantAuditLevel.SAFE => Severity.Success,
            _ => Severity.Info,
        };

        public Color GetColor() => level switch
        {
            AssistantAuditLevel.DANGEROUS => Color.Error,
            AssistantAuditLevel.CAUTION => Color.Warning,
            AssistantAuditLevel.SAFE => Color.Success,
            _ => Color.Default,
        };

        public string GetIcon() => level switch
        {
            AssistantAuditLevel.DANGEROUS => Icons.Material.Filled.Dangerous,
            AssistantAuditLevel.CAUTION => Icons.Material.Filled.Warning,
            AssistantAuditLevel.SAFE => Icons.Material.Filled.Verified,
            _ => Icons.Material.Filled.HelpOutline,
        };
    }

    /// <summary>
    /// Parses an audit level string and falls back to <see cref="AssistantAuditLevel.UNKNOWN"/> when parsing fails.
    /// </summary>
    /// <param name="value">The audit level text to parse.</param>
    /// <returns>The parsed audit level, or <see cref="AssistantAuditLevel.UNKNOWN"/> for null, empty, or invalid values.</returns>
    public static AssistantAuditLevel Parse(string? value) => Enum.TryParse<AssistantAuditLevel>(value, true, out var level) ? level : AssistantAuditLevel.UNKNOWN;
}
