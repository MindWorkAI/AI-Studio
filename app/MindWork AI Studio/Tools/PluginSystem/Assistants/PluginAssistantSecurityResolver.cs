using AIStudio.Agents.AssistantAudit;
using AIStudio.Settings;

namespace AIStudio.Tools.PluginSystem.Assistants;


public static class PluginAssistantSecurityResolver
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginAssistantSecurityResolver).Namespace, nameof(PluginAssistantSecurityResolver));

    private static string GetAvailabilityLabel(bool requiresAudit, bool hasAudit, bool hasHashMismatch, bool isBlocked, bool canOverride)
    {
        if (hasHashMismatch)
            return TB("Changed");
        if (requiresAudit)
            return TB("Audit Required");
        if (!hasAudit)
            return TB("Not Audited");
        if (isBlocked)
            return TB("Blocked");
        if (canOverride)
            return TB("Restricted");

        return TB("Unlocked");
    }

    private static Color GetAvailabilityColor(bool requiresAudit, bool hasAudit, bool hasHashMismatch, bool isBlocked, bool canOverride)
    {
        if (hasHashMismatch || requiresAudit)
            return Color.Warning;
        if (isBlocked)
            return Color.Default;
        if (!hasAudit || canOverride)
            return Color.Default;

        return Color.Success;
    }

    private static string GetAvailabilityIcon(bool requiresAudit, bool hasAudit, bool hasHashMismatch, bool isBlocked, bool canOverride)
    {
        if (hasHashMismatch)
            return MudBlazor.Icons.Material.Filled.Warning;
        if (requiresAudit)
            return MudBlazor.Icons.Material.Filled.GppMaybe;
        if (!hasAudit)
            return MudBlazor.Icons.Material.Filled.HelpOutline;
        if (isBlocked)
            return MudBlazor.Icons.Material.Filled.Lock;
        if (canOverride)
            return MudBlazor.Icons.Material.Filled.ReportProblem;

        return MudBlazor.Icons.Material.Filled.LockOpen;
    }
    
    private static string GetSecurityBadgeIcon(bool requiresAudit, bool hasAudit, bool hasHashMismatch, bool isBlocked, bool canOverride)
    {
        if (hasHashMismatch)
            return MudBlazor.Icons.Material.Filled.RemoveModerator;
        if (!hasAudit)
            return MudBlazor.Icons.Material.Filled.AddModerator;

        return MudBlazor.Icons.Material.Filled.Security;
    }

    /// <summary>
    /// Resolves the effective security state for an assistant plugin.
    /// Possible outcomes are: no audit stored yet, plugin changed since the last audit,
    /// audited but below the configured minimum level and therefore either blocked or manually overridable,
    /// or audited, unchanged, and fully unlocked.
    /// </summary>
    public static PluginAssistantSecurityState Resolve(SettingsManager settingsManager, PluginAssistants plugin)
    {
        var auditSettings = settingsManager.ConfigurationData.AssistantPluginAudit;
        var currentHash = plugin.ComputeAuditHash();
        var audit = settingsManager.ConfigurationData.AssistantPluginAudits.FirstOrDefault(x => x.PluginId == plugin.Id);
        var hasAudit = audit is not null && audit.Level is not AssistantAuditLevel.UNKNOWN;
        var hashMatches = hasAudit && string.Equals(audit!.PluginHash, currentHash, StringComparison.Ordinal);
        var hasHashMismatch = hasAudit && !hashMatches;
        var isBelowMinimum = hashMatches && audit is not null && audit.Level < auditSettings.MinimumLevel;
        var meetsMinimum = hashMatches && audit is not null && audit.Level >= auditSettings.MinimumLevel;
        var requiresAudit = hasHashMismatch || auditSettings.RequireAuditBeforeActivation && !hasAudit;
        var isBlocked = requiresAudit || isBelowMinimum && auditSettings.BlockActivationBelowMinimum;
        var canOverride = isBelowMinimum && !auditSettings.BlockActivationBelowMinimum;
        var canUsePlugin = !isBlocked;

        if (!hasAudit)
        {
            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = null,
                Settings = auditSettings,
                CurrentHash = currentHash,
                HashMatches = false,
                HasHashMismatch = false,
                IsBelowMinimum = false,
                MeetsMinimumLevel = false,
                RequiresAudit = requiresAudit,
                IsBlocked = isBlocked,
                CanOverride = false,
                CanActivatePlugin = !isBlocked,
                CanStartAssistant = !isBlocked,
                AuditLabel = TB("Unknown"),
                AuditColor = AssistantAuditLevel.UNKNOWN.GetColor(),
                AuditIcon = AssistantAuditLevel.UNKNOWN.GetIcon(),
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityColor = GetAvailabilityColor(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                StatusLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                Headline = requiresAudit ? TB("This assistant is currently locked.") : TB("This assistant currently has no stored audit."),
                Description = requiresAudit
                    ? TB("No security audit exists yet, and your current security settings require one before this assistant plugin may be enabled or used.")
                    : TB("No security audit exists yet. Your current security settings do not require an audit before this assistant plugin may be used."),
                StatusColor = GetAvailabilityColor(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                StatusIcon = GetAvailabilityIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                ActionLabel = TB("Start Security Check"),
            };
        }

        if (hasHashMismatch)
        {
            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = audit,
                Settings = auditSettings,
                CurrentHash = currentHash,
                HashMatches = false,
                HasHashMismatch = true,
                IsBelowMinimum = false,
                MeetsMinimumLevel = false,
                RequiresAudit = true,
                IsBlocked = true,
                CanOverride = false,
                CanActivatePlugin = false,
                CanStartAssistant = false,
                AuditLabel = TB("Unknown"),
                AuditColor = AssistantAuditLevel.UNKNOWN.GetColor(),
                AuditIcon = AssistantAuditLevel.UNKNOWN.GetIcon(),
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                AvailabilityColor = GetAvailabilityColor(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                StatusLabel = GetAvailabilityLabel(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                Headline = TB("This assistant is locked until it is audited again."),
                Description = TB("The plugin code changed after the last security audit. The stored result no longer matches the current code, so this assistant plugin must be audited again before it may be enabled or used."),
                StatusColor = GetAvailabilityColor(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                StatusIcon = GetAvailabilityIcon(requiresAudit: true, hasAudit, hasHashMismatch, isBlocked: true, canOverride: false),
                ActionLabel = TB("Run Security Check Again"),
            };
        }

        if (isBelowMinimum)
        {
            var isBlockedByMinimum = auditSettings.BlockActivationBelowMinimum;
            var auditLevel = audit!.Level;

            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = audit,
                Settings = auditSettings,
                CurrentHash = currentHash,
                HashMatches = true,
                HasHashMismatch = false,
                IsBelowMinimum = true,
                MeetsMinimumLevel = false,
                RequiresAudit = false,
                IsBlocked = isBlockedByMinimum,
                CanOverride = !isBlockedByMinimum,
                CanActivatePlugin = !isBlockedByMinimum,
                CanStartAssistant = !isBlockedByMinimum,
                AuditLabel = auditLevel.GetName(),
                AuditColor = auditLevel.GetColor(),
                AuditIcon = auditLevel.GetIcon(),
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                AvailabilityColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                StatusLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                Headline = isBlockedByMinimum ? TB("This assistant is currently locked.") : TB("This assistant can still be used because your settings allow it."),
                Description = isBlockedByMinimum
                    ? string.Format(TB("The current audit result '{0}' is below your required minimum level '{1}'. Your security settings therefore block this assistant plugin."), auditLevel.GetName(), auditSettings.MinimumLevel.GetName())
                    : string.Format(TB("The current audit result is '{0}', which is below your required minimum level '{1}'. Your settings still allow manual activation, but the assistant keeps this security status and should be reviewed carefully."), auditLevel.GetName(), auditSettings.MinimumLevel.GetName()),
                StatusColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                StatusIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                ActionLabel = TB("Open Security Check"),
            };
        }

        var auditLevelDefault = audit!.Level;

        return new PluginAssistantSecurityState
        {
            Plugin = plugin,
            Audit = audit,
            Settings = auditSettings,
            CurrentHash = currentHash,
            HashMatches = true,
            HasHashMismatch = false,
            IsBelowMinimum = false,
            MeetsMinimumLevel = meetsMinimum,
            RequiresAudit = false,
            IsBlocked = false,
            CanOverride = false,
            CanActivatePlugin = canUsePlugin,
            CanStartAssistant = canUsePlugin,
            AuditLabel = auditLevelDefault.GetName(),
            AuditColor = auditLevelDefault.GetColor(),
            AuditIcon = auditLevelDefault.GetIcon(),
            AvailabilityLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            AvailabilityColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            AvailabilityIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            StatusLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            BadgeIcon = GetSecurityBadgeIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            Headline = TB("This assistant is currently unlocked."),
            Description = string.Format(TB("The stored audit matches the current plugin code and meets your required minimum level '{0}'."), auditSettings.MinimumLevel.GetName()),
            StatusColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            StatusIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            ActionLabel = TB("Open Security Check"),
        };
    }
}
