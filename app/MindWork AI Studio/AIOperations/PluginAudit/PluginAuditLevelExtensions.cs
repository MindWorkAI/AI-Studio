using AIStudio.Tools.PluginSystem;

namespace AIStudio.AIOperations.PluginAudit;

public static class PluginAuditLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginAuditLevelExtensions).Namespace, nameof(PluginAuditLevelExtensions));

    extension(PluginAuditLevel level)
    {
        public string Name => level switch
        {
            PluginAuditLevel.DANGEROUS => TB("Dangerous"),
            PluginAuditLevel.CAUTION => TB("Concerning"),
            PluginAuditLevel.SAFE => TB("Safe"),
            _ => TB("Unknown"),
        };

        public Severity Severity => level switch
        {
            PluginAuditLevel.DANGEROUS => Severity.Error,
            PluginAuditLevel.CAUTION => Severity.Warning,
            PluginAuditLevel.SAFE => Severity.Success,
            _ => Severity.Info,
        };

        public Color Color => level switch
        {
            PluginAuditLevel.DANGEROUS => Color.Error,
            PluginAuditLevel.CAUTION => Color.Warning,
            PluginAuditLevel.SAFE => Color.Success,
            _ => Color.Default,
        };

        public string Icon => level switch
        {
            PluginAuditLevel.DANGEROUS => AppIcons.SECURITY_DANGEROUS,
            PluginAuditLevel.CAUTION => AppIcons.WARNING,
            PluginAuditLevel.SAFE => AppIcons.AUDIT_SAFE,
            _ => AppIcons.UNKNOWN,
        };

        /// <summary>
        /// Parses an audit level string and falls back to <see cref="PluginAuditLevel.UNKNOWN"/> when parsing fails.
        /// </summary>
        /// <param name="value">The audit level text to parse.</param>
        /// <returns>The parsed audit level, or <see cref="PluginAuditLevel.UNKNOWN"/> for null, empty, or invalid values.</returns>
        public static PluginAuditLevel ParseOrUnknown(string? value) => Enum.TryParse<PluginAuditLevel>(value, true, out var parsedLevel) ? parsedLevel : PluginAuditLevel.UNKNOWN;
    }
}