using AIStudio.AIOperations;
using AIStudio.AIOperations.PluginAudit;
using AIStudio.Provider;
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
            await MessageBus.INSTANCE.SendError(new (AppIcons.AI_OPERATION, GetMissingProviderMessage(issue)));
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
    /// Audits every running assistant plugin which needs an audit, as long as automatic audits are switched on.
    /// </summary>
    /// <remarks>
    /// In the background, there is no provider at hand. Should no provider be left for a plugin, its audit would
    /// fail without a request, and the user would get two messages for each plugin, again with every visit of the
    /// assistants or plugins page. Such a plugin is therefore skipped, and a single message says why.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The audits which succeeded, for the caller to store.</returns>
    public async Task<IReadOnlyList<PluginAssistantAudit>> RunAutomaticAuditsAsync(CancellationToken token = default)
    {
        if (!settingsManager.ConfigurationData.AssistantPluginAudit.AutomaticallyAuditAssistants)
            return [];

        var audits = new List<PluginAssistantAudit>();
        AIOperationProviderIssue? issueOfSkippedPlugins = null;
        foreach (var plugin in PluginFactory.RunningPlugins.OfType<PluginAssistants>().ToList())
        {
            if (!PluginAssistantSecurityResolver.Resolve(settingsManager, plugin).RequiresAudit)
                continue;

            var choice = this.PreviewProvider(new PluginAuditRequest(plugin));
            if (!choice.HasProvider)
            {
                issueOfSkippedPlugins ??= GetRelevantIssue(choice);
                continue;
            }

            var audit = await this.RunAuditAsync(plugin, token: token);
            if (audit.Level is PluginAuditLevel.UNKNOWN)
            {
                await MessageBus.INSTANCE.SendError(new (AppIcons.AI_OPERATION, string.Format(TA("The automatic security audit for the assistant plugin '{0}' failed. Please run it manually on the plugins page."), plugin.Name)));
                continue;
            }

            audits.Add(audit);
        }

        if (issueOfSkippedPlugins is { } issue)
            await MessageBus.INSTANCE.SendError(new (AppIcons.AI_OPERATION, GetAutomaticAuditMessage(issue)));

        return audits;
    }

    /// <summary>
    /// Keeps an audit in the settings: it replaces the audit stored for the same plugin, in its place, or joins the others.
    /// </summary>
    /// <remarks>
    /// The settings keep one audit per plugin, whatever version of the plugin it checked: a newer audit replaces
    /// the older one, also when the plugin changed meanwhile. The settings are not written; the caller stores
    /// them once it changed everything it wanted to.
    /// </remarks>
    /// <param name="settingsManager">The settings which keep the audits.</param>
    /// <param name="audit">The audit to keep.</param>
    public static void UpsertAudit(SettingsManager settingsManager, PluginAssistantAudit audit)
    {
        var audits = settingsManager.ConfigurationData.AssistantPluginAudits;
        var existingIndex = audits.FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }

    /// <summary>
    /// Which provider an audit would run with when nobody has a provider at hand, as in the background.
    /// </summary>
    /// <param name="request">The audit, for the confidence its content requires; null for the least any audit requires.</param>
    /// <returns>The choice, which logs nothing.</returns>
    public AIOperationProviderChoice PreviewProvider(PluginAuditRequest? request = null)
    {
        var requiredConfidence = request is null
            ? ConfidenceLevel.NONE
            : auditOperation.GetRequiredConfidence(request);

        return providerResolver.Preview(PluginAuditOperation.GetProviderSettings(settingsManager), requiredConfidence, GetProviderOptions(settingsManager, null));
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

    private static string GetAutomaticAuditMessage(AIOperationProviderIssue issue) => issue switch
    {
        AIOperationProviderIssue.NOT_SET or AIOperationProviderIssue.MISSING => TA("AI Studio did not audit new or updated assistant plugins automatically, because no model is set for security checks, and there is no app-wide default either. Choose a model for security checks in the settings."),
        AIOperationProviderIssue.PROHIBITED => TA("AI Studio did not audit new or updated assistant plugins automatically, because your organization does not allow the model for security checks. Choose another one in the settings."),
        _ => TA("AI Studio did not audit new or updated assistant plugins automatically, because the model for security checks is not trusted enough. Choose another one in the settings."),
    };

    private static string GetMissingProviderSummary(AIOperationProviderIssue issue) => issue switch
    {
        AIOperationProviderIssue.NOT_SET or AIOperationProviderIssue.MISSING => TB("No audit provider is configured."),
        AIOperationProviderIssue.PROHIBITED => TB("The organization does not allow the provider for security checks."),
        _ => TB("The provider is not trusted enough for security checks."),
    };
}
