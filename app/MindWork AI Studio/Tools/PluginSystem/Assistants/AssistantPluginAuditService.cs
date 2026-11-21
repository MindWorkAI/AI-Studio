using AIStudio.AIOperations;
using AIStudio.AIOperations.PluginAudit;
using AIStudio.Settings;

namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Runs the security audit of an assistant plugin and maps its result to the persisted audit model.
/// </summary>
public sealed class AssistantPluginAuditService(PluginAuditOperation auditOperation, AIOperationProviderResolver providerResolver, SettingsManager settingsManager)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantPluginAuditService).Namespace, nameof(AssistantPluginAuditService));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(AssistantPluginAuditService).Namespace, nameof(AssistantPluginAuditService));

    /// <summary>
    /// Runs an assistant plugin audit, optionally falling back to the supplied provider when no audit provider is configured.
    /// </summary>
    /// <param name="plugin">The plugin to audit.</param>
    /// <param name="providerAtHand">A provider somebody picked for something else, e.g., the assistant they are building; the audit falls back on it last.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The audit, which names the provider it ran with.</returns>
    public async Task<PluginAssistantAudit> RunAuditAsync(PluginAssistants plugin, Settings.Provider? providerAtHand = null, CancellationToken token = default)
    {
        var request = new PluginAuditRequest(plugin);
        var choice = providerResolver.Resolve(auditOperation, request, PluginAuditOperation.GetProviderSettings(settingsManager), GetProviderOptions(settingsManager, providerAtHand));

        PluginAuditResult result;
        if (choice.HasProvider)
            result = await auditOperation.RunAsync(request, new(choice.Provider), token);
        else
        {
            var issue = GetRelevantIssue(choice);
            await MessageBus.INSTANCE.SendError(new (AppIcons.AGENT, GetMissingProviderMessage(issue)));
            result = new PluginAuditResult
            {
                Level = nameof(PluginAuditLevel.UNKNOWN),
                Summary = GetMissingProviderSummary(issue),
            };
        }

        var provider = choice.Provider;
        var promptPreview = await plugin.BuildAuditPromptPreviewAsync(token);
        return new PluginAssistantAudit
        {
            PluginId = plugin.Id,
            PluginHash = plugin.ComputeAuditHash(),
            AuditedAtUtc = DateTimeOffset.UtcNow,
            AuditProviderId = provider.Id,
            AuditProviderName = provider == Settings.Provider.NONE
                ? string.Empty
                : provider.InstanceName,
            Level = PluginAuditLevel.ParseOrUnknown(result.Level),
            Summary = result.Summary,
            Confidence = result.Confidence,
            PromptPreview = promptPreview,
            Findings = result.Findings,
        };
    }

    /// <summary>
    /// What an audit adds to the choice of its provider.
    /// </summary>
    /// <remarks>
    /// Without a provider of its own, an audit falls back on the app-wide default, and then on the provider at
    /// hand. A provider somebody picked for something else -- the assistant they were building, the revision
    /// they asked for, the check they are standing in front of -- may read the source of a plugin only when it
    /// clears the bar of the audit. Otherwise a provider an organization ruled out for audits would see the very
    /// thing it was ruled out for.
    /// </remarks>
    /// <param name="settingsManager">The settings, for the app-wide default.</param>
    /// <param name="providerAtHand">The provider at hand, or null.</param>
    /// <returns>The options for the provider resolver.</returns>
    internal static AIOperationProviderOptions GetProviderOptions(SettingsManager settingsManager, Settings.Provider? providerAtHand) => new()
    {
        FallbackProviderIds = [settingsManager.AppDefaultProviderId, providerAtHand?.Id],
        OwnMinimumConfidenceAppliesToFallbacks = true,
    };

    /// <summary>
    /// The issue which explains best why no provider can audit: the one of the provider somebody named.
    /// </summary>
    /// <remarks>
    /// The first named fallback is the app-wide default, or the provider at hand when there is no default.
    /// When no fallback was named at all, the preferred provider of the audit is what failed.
    /// </remarks>
    /// <param name="choice">The choice without a provider.</param>
    /// <returns>The issue to explain; NOT_SET when nobody named any provider.</returns>
    internal static AIOperationProviderIssue GetRelevantIssue(AIOperationProviderChoice choice) => choice.FallbackIssue is AIOperationProviderIssue.NOT_SET
        ? choice.PreferredIssue
        : choice.FallbackIssue;

    private static string GetMissingProviderMessage(AIOperationProviderIssue issue) => issue switch
    {
        AIOperationProviderIssue.NOT_SET or AIOperationProviderIssue.MISSING => TB("No provider is configured for security checks."),
        AIOperationProviderIssue.PROHIBITED => TA("Your organization does not allow the provider for security checks. Pick another one, or choose a dedicated provider for security checks in the app settings."),
        _ => TA("The selected provider is not trusted enough for security checks. Pick one which meets the confidence required here, or choose a dedicated provider for security checks in the app settings."),
    };

    private static string GetMissingProviderSummary(AIOperationProviderIssue issue) => issue switch
    {
        AIOperationProviderIssue.NOT_SET or AIOperationProviderIssue.MISSING => TB("No audit provider is configured."),
        AIOperationProviderIssue.PROHIBITED => TB("The organization does not allow the provider for security checks."),
        _ => TB("The provider is not trusted enough for security checks."),
    };
}
