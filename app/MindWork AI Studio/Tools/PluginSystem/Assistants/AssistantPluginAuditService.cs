using AIStudio.Agents.AssistantAudit;

namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Runs an assistant security audit and maps the agent result to the persisted audit model.
/// </summary>
public sealed class AssistantPluginAuditService(AssistantAuditAgent auditAgent)
{
    /// <summary>
    /// Runs an assistant plugin audit, optionally falling back to the supplied provider when no audit provider is configured.
    /// </summary>
    public async Task<PluginAssistantAudit> RunAuditAsync(PluginAssistants plugin, CancellationToken token = default, Settings.Provider? fallbackProvider = null)
    {
        var result = await auditAgent.AuditAsync(plugin, token, fallbackProvider);
        var provider = auditAgent.ProviderSettings;
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
            Level = AssistantAuditLevelExtensions.Parse(result.Level),
            Summary = result.Summary,
            Confidence = result.Confidence,
            PromptPreview = promptPreview,
            Findings = result.Findings,
        };
    }
}
