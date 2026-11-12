using AIStudio.Tools.PluginSystem;

namespace AIStudio.Agents.AssistantAudit;

public static class AssistantAuditLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantAuditLevelExtensions).Namespace, nameof(AssistantAuditLevelExtensions));

    extension(AssistantAuditLevel level)
    {
        public string Name => level switch
        {
            AssistantAuditLevel.DANGEROUS => TB("Dangerous"),
            AssistantAuditLevel.CAUTION => TB("Concerning"),
            AssistantAuditLevel.SAFE => TB("Safe"),
            _ => TB("Unknown"),
        };

        public Severity Severity => level switch
        {
            AssistantAuditLevel.DANGEROUS => Severity.Error,
            AssistantAuditLevel.CAUTION => Severity.Warning,
            AssistantAuditLevel.SAFE => Severity.Success,
            _ => Severity.Info,
        };

        public Color Color => level switch
        {
            AssistantAuditLevel.DANGEROUS => Color.Error,
            AssistantAuditLevel.CAUTION => Color.Warning,
            AssistantAuditLevel.SAFE => Color.Success,
            _ => Color.Default,
        };

        public string Icon => level switch
        {
            AssistantAuditLevel.DANGEROUS => AppIcons.SECURITY_DANGEROUS,
            AssistantAuditLevel.CAUTION => AppIcons.WARNING,
            AssistantAuditLevel.SAFE => AppIcons.AUDIT_SAFE,
            _ => AppIcons.UNKNOWN,
        };

        /// <summary>
        /// Parses an audit level string and falls back to <see cref="AssistantAuditLevel.UNKNOWN"/> when parsing fails.
        /// </summary>
        /// <param name="value">The audit level text to parse.</param>
        /// <returns>The parsed audit level, or <see cref="AssistantAuditLevel.UNKNOWN"/> for null, empty, or invalid values.</returns>
        public static AssistantAuditLevel ParseOrUnknown(string? value) => Enum.TryParse<AssistantAuditLevel>(value, true, out var parsedLevel) ? parsedLevel : AssistantAuditLevel.UNKNOWN;
    }
}
