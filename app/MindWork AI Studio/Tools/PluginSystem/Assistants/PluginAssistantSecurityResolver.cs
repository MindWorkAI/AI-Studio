using AIStudio.Agents.AssistantAudit;
using AIStudio.Settings;

namespace AIStudio.Tools.PluginSystem.Assistants;


public static class PluginAssistantSecurityResolver
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginAssistantSecurityResolver).Namespace, nameof(PluginAssistantSecurityResolver));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(PluginAssistantSecurityResolver).Namespace, nameof(PluginAssistantSecurityResolver));

    private static string NormalizeHash(string hash) => string.IsNullOrWhiteSpace(hash) ? string.Empty : hash.Trim().ToUpperInvariant();

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
            return AppIcons.WARNING;
        if (requiresAudit)
            return AppIcons.SECURITY_WARNING;
        if (!hasAudit)
            return AppIcons.UNKNOWN;
        if (isBlocked)
            return AppIcons.LOCK;
        if (canOverride)
            return AppIcons.WARNING;

        return AppIcons.UNLOCK;
    }
    
    // ReSharper disable UnusedParameter.Local
    private static string GetSecurityBadgeIcon(bool requiresAudit, bool hasAudit, bool hasHashMismatch, bool isBlocked, bool canOverride)
    {
        if (hasHashMismatch)
            return AppIcons.AUDIT_MISMATCH;
        if (!hasAudit)
            return AppIcons.SECURITY_NOT_AUDITED;

        // An audit below the required minimum level never shows the check mark of a passed audit:
        if (isBlocked || canOverride)
            return AppIcons.SECURITY_AUDIT_FAILED;

        return AppIcons.SECURITY_AUDIT_PASSED;
    }
    // ReSharper restore UnusedParameter.Local

    /// <summary>
    /// Resolves the effective security state for an assistant plugin.
    /// Possible outcomes are: no audit stored yet, plugin changed since the last audit,
    /// audited but below the configured minimum level and therefore either blocked or manually overridable,
    /// or audited, unchanged, and fully unlocked.
    /// </summary>
    public static PluginAssistantSecurityState Resolve(SettingsManager settingsManager, PluginAssistants plugin)
    {
        var auditSettings = settingsManager.ConfigurationData.AssistantPluginAudit;
        var enforceAuditBeforeActivation = auditSettings.RequireAuditBeforeActivation;
        var isEnforcementDisabled = !enforceAuditBeforeActivation;
        var currentHash = NormalizeHash(plugin.ComputeAuditHash());
        var enterpriseApproval = auditSettings.EnterpriseApprovedPlugins
            .FirstOrDefault(x => string.Equals(NormalizeHash(x.PluginHash), currentHash, StringComparison.Ordinal));

        if (enterpriseApproval is not null)
        {
            var enterpriseHeadline = string.IsNullOrWhiteSpace(enterpriseApproval.DisplayName)
                ? TA("This assistant was approved by your organization.")
                : string.Format(TA("This assistant was approved by your organization as '{0}'."), enterpriseApproval.DisplayName);

            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = null,
                EnterpriseApproval = enterpriseApproval,
                Settings = auditSettings,
                Source = PluginAssistantSecurityStatusSource.ENTERPRISE_APPROVAL,
                CurrentHash = currentHash,
                HashMatches = true,
                HasHashMismatch = false,
                IsBelowMinimum = false,
                MeetsMinimumLevel = true,
                RequiresAudit = false,
                IsBlocked = false,
                CanOverride = false,
                CanActivatePlugin = true,
                CanStartAssistant = true,
                AuditLabel = TB("Safe"),
                AuditColor = AssistantAuditLevel.SAFE.Color,
                AuditIcon = AssistantAuditLevel.SAFE.Icon,
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit: true, hasHashMismatch: false, isBlocked: false, canOverride: false),
                AvailabilityColor = GetAvailabilityColor(requiresAudit: false, hasAudit: true, hasHashMismatch: false, isBlocked: false, canOverride: false),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit: true, hasHashMismatch: false, isBlocked: false, canOverride: false),
                StatusLabel = TB("Unlocked"),
                SourceLabel = TA("Approved by your organization"),
                SourceColor = Color.Success,
                SourceIcon = AppIcons.ORGANIZATION,
                BadgeIcon = AppIcons.ORGANIZATION,
                Headline = enterpriseHeadline,
                Description = TB("The current plugin hash matches an enterprise-managed approval. No manual security audit is required for activation or usage."),
                StatusColor = Color.Success,
                StatusIcon = AppIcons.TRUSTED,
                ActionLabel = TB("Open Security Details"),
            };
        }

        var audit = settingsManager.ConfigurationData.AssistantPluginAudits.FirstOrDefault(x => x.PluginId == plugin.Id);
        var hasAudit = audit is not null && audit.Level is not AssistantAuditLevel.UNKNOWN;
        var hashMatches = hasAudit && string.Equals(NormalizeHash(audit!.PluginHash), currentHash, StringComparison.Ordinal);
        var hasHashMismatch = hasAudit && !hashMatches;
        var isBelowMinimum = hashMatches && audit is not null && audit.Level < auditSettings.MinimumLevel;
        var meetsMinimum = hashMatches && audit is not null && audit.Level >= auditSettings.MinimumLevel;
        var requiresAudit = enforceAuditBeforeActivation && (hasHashMismatch || !hasAudit);
        var isBlocked = requiresAudit || enforceAuditBeforeActivation && isBelowMinimum && auditSettings.BlockActivationBelowMinimum;
        var canOverride = isBelowMinimum && (!auditSettings.BlockActivationBelowMinimum || isEnforcementDisabled);
        var canUsePlugin = !isBlocked;

        if (!hasAudit)
        {
            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = null,
                Settings = auditSettings,
                Source = PluginAssistantSecurityStatusSource.NONE,
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
                AuditColor = AssistantAuditLevel.UNKNOWN.Color,
                AuditIcon = AssistantAuditLevel.UNKNOWN.Icon,
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityColor = GetAvailabilityColor(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                StatusLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                SourceLabel = TB("No Approval"),
                SourceColor = Color.Default,
                SourceIcon = AppIcons.UNKNOWN,
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                Headline = requiresAudit ? TB("This assistant is currently locked.") : TB("This assistant currently has no stored audit."),
                Description = requiresAudit
                    ? TA("No security audit exists yet, and your current security settings require one before this assistant plugin may be enabled or used.")
                    : TA("No security audit exists yet. Your current security settings do not require an audit before this assistant plugin may be used."),
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
                Source = PluginAssistantSecurityStatusSource.NONE,
                CurrentHash = currentHash,
                HashMatches = false,
                HasHashMismatch = true,
                IsBelowMinimum = false,
                MeetsMinimumLevel = false,
                RequiresAudit = requiresAudit,
                IsBlocked = isBlocked,
                CanOverride = false,
                CanActivatePlugin = !isBlocked,
                CanStartAssistant = !isBlocked,
                AuditLabel = TB("Unknown"),
                AuditColor = AssistantAuditLevel.UNKNOWN.Color,
                AuditIcon = AssistantAuditLevel.UNKNOWN.Icon,
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityColor = GetAvailabilityColor(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                StatusLabel = GetAvailabilityLabel(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                SourceLabel = TB("No Approval"),
                SourceColor = Color.Default,
                SourceIcon = AppIcons.WARNING,
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                Headline = requiresAudit ? TB("This assistant is locked until it is audited again.") : TB("This assistant changed after its last audit."),
                Description = requiresAudit
                    ? TB("The plugin code changed after the last security audit. The stored result no longer matches the current code, so this assistant plugin must be audited again before it may be enabled or used.")
                    : TB("The plugin code changed after the last security audit. Audit enforcement is currently disabled, so this assistant plugin can still be enabled or used."),
                StatusColor = GetAvailabilityColor(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                StatusIcon = GetAvailabilityIcon(requiresAudit, hasAudit, hasHashMismatch, isBlocked, canOverride: false),
                ActionLabel = TB("Run Security Check Again"),
            };
        }

        if (isBelowMinimum)
        {
            var isBlockedByMinimum = enforceAuditBeforeActivation && auditSettings.BlockActivationBelowMinimum;
            var auditLevel = audit!.Level;

            return new PluginAssistantSecurityState
            {
                Plugin = plugin,
                Audit = audit,
                Settings = auditSettings,
                Source = PluginAssistantSecurityStatusSource.USER_AUDIT,
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
                AuditLabel = auditLevel.Name,
                AuditColor = auditLevel.Color,
                AuditIcon = auditLevel.Icon,
                AvailabilityLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                AvailabilityColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                AvailabilityIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                StatusLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                SourceLabel = TB("User Audit"),
                SourceColor = auditLevel.Color,
                SourceIcon = AppIcons.SECURITY_AUDIT_FAILED,
                BadgeIcon = GetSecurityBadgeIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlockedByMinimum, canOverride),
                Headline = isBlockedByMinimum
                    ? TB("This assistant is currently locked.")
                    : isEnforcementDisabled
                        ? TB("This assistant can still be used because audit enforcement is disabled.")
                        : TA("This assistant can still be used because your settings allow it."),
                Description = isBlockedByMinimum
                    ? string.Format(TA("The current audit result '{0}' is below your required minimum level '{1}'. Your security settings therefore block this assistant plugin."), auditLevel.Name, auditSettings.MinimumLevel.Name)
                    : isEnforcementDisabled
                        ? string.Format(TA("The current audit result is '{0}', which is below your required minimum level '{1}'. Audit enforcement is currently disabled, so this assistant plugin can still be enabled or used."), auditLevel.Name, auditSettings.MinimumLevel.Name)
                        : string.Format(TA("The current audit result is '{0}', which is below your required minimum level '{1}'. Your settings still allow manual activation, but the assistant keeps this security status and should be reviewed carefully."), auditLevel.Name, auditSettings.MinimumLevel.Name),
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
            Source = PluginAssistantSecurityStatusSource.USER_AUDIT,
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
            AuditLabel = auditLevelDefault.Name,
            AuditColor = auditLevelDefault.Color,
            AuditIcon = auditLevelDefault.Icon,
            AvailabilityLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            AvailabilityColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            AvailabilityIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            StatusLabel = GetAvailabilityLabel(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            SourceLabel = TB("User Audit"),
            SourceColor = auditLevelDefault.Color,
            SourceIcon = AppIcons.SECURITY_AUDIT_PASSED,
            BadgeIcon = GetSecurityBadgeIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            Headline = TB("This assistant is currently unlocked."),
            Description = string.Format(TA("The stored audit matches the current plugin code and meets your required minimum level '{0}'."), auditSettings.MinimumLevel.Name),
            StatusColor = GetAvailabilityColor(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            StatusIcon = GetAvailabilityIcon(requiresAudit: false, hasAudit, hasHashMismatch: false, isBlocked: false, canOverride: false),
            ActionLabel = TB("Open Security Check"),
        };
    }
}
