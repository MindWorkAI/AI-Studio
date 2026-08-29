using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    /// <summary>
    /// The assistant plugins your organization enabled without leaving the user a way to switch them off.
    /// </summary>
    /// <remarks>
    /// This is deliberately not persisted. Such an activation is decided live from the approvals of
    /// your organization, so it ends the moment the approval does, without anything to clean up. The
    /// field is replaced as a whole instead of being edited in place, so a reload never lets the user
    /// interface observe a half-built state.
    /// </remarks>
    private static IReadOnlySet<Guid> ENFORCED_ASSISTANT_ACTIVATIONS = new HashSet<Guid>();

    /// <summary>
    /// Whether your organization requires this assistant plugin to stay enabled.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin in question.</param>
    /// <returns>True when the user may not switch this assistant plugin off.</returns>
    public static bool IsAssistantActivationEnforced(Guid pluginId) => ENFORCED_ASSISTANT_ACTIVATIONS.Contains(pluginId);

    /// <summary>
    /// Applies what the approvals of your organization say about enabling assistant plugins.
    /// </summary>
    /// <remarks>
    /// Approving an assistant plugin only states that it is safe. Whether it is enabled is a second
    /// decision, and an organization expresses it with the Activate field of an approval. Without that
    /// field nothing changes: the plugin is approved, and the user switches it on.<br/><br/>
    /// We read the approvals as they are stored, which is the same source the security card uses. They
    /// survive a configuration plugin which failed to load, so one broken configuration cannot
    /// silently withdraw what an organization enabled.<br/><br/>
    /// Call this once all plugins are running and the effective approvals were recomputed.
    /// </remarks>
    /// <returns>True when the settings were changed and have to be stored, otherwise false.</returns>
    private static bool RefreshEnterpriseAssistantActivations()
    {
        var approvalsByHash = new Dictionary<string, DataAssistantPluginEnterpriseApproval>(StringComparer.Ordinal);
        foreach (var approval in SettingsManagerAccess.ConfigurationData.AssistantPluginAudit.EnterpriseApprovedPlugins)
            approvalsByHash[NormalizeAssistantHash(approval.PluginHash)] = approval;

        var appliedActivations = SettingsManagerAccess.ConfigurationData.AppliedEnterpriseAssistantActivations;
        var enforcedActivations = new HashSet<Guid>();
        var wasConfigurationChanged = false;

        foreach (var assistantPlugin in RUNNING_PLUGINS.OfType<PluginAssistants>())
        {
            var pluginHash = NormalizeAssistantHash(assistantPlugin.ComputeAuditHash());
            if (!approvalsByHash.TryGetValue(pluginHash, out var approval) || !approval.Activate)
                continue;

            //
            // An approval is matched by its hash alone, without looking at where the plugin is stored:
            // a plugin the user placed themselves counts as approved as soon as its Lua files are the
            // ones the organization approved. For an approval that is right, because the hash is the
            // code. For enabling a plugin on the user's behalf it is not enough: the organization would
            // then enforce a copy it never rolled out, cannot update, and cannot withdraw again. So we
            // ask for the rollout in addition to the approval:
            //
            var pluginMetadata = AVAILABLE_PLUGINS.FirstOrDefault(plugin => plugin.Id == assistantPlugin.Id);
            if (pluginMetadata is not { IsManagedByConfigServer: true })
            {
                LOG.LogInformation($"Your organization asks for the assistant plugin '{assistantPlugin.Name}' (id '{assistantPlugin.Id}') to be enabled, but it did not deploy this copy of the plugin. Ignoring the activation: the approval stays in place, and you decide about enabling it.");
                continue;
            }

            if (!approval.AllowUserOverride)
            {
                enforcedActivations.Add(assistantPlugin.Id);
                LOG.LogInformation($"Your organization requires the assistant plugin '{assistantPlugin.Name}' (id '{assistantPlugin.Id}') to stay enabled.");
                continue;
            }

            // An organization default is applied once. Afterwards the decision belongs to the user:
            if (appliedActivations.Contains(pluginHash))
                continue;

            appliedActivations.Add(pluginHash);
            wasConfigurationChanged = true;

            if (SettingsManagerAccess.ConfigurationData.EnabledPlugins.Contains(assistantPlugin.Id))
                continue;

            SettingsManagerAccess.ConfigurationData.EnabledPlugins.Add(assistantPlugin.Id);
            LOG.LogInformation($"Enabled the assistant plugin '{assistantPlugin.Name}' (id '{assistantPlugin.Id}') because your organization enables it by default. You may switch it off again.");
        }

        ENFORCED_ASSISTANT_ACTIVATIONS = enforcedActivations;

        //
        // Forget the defaults we applied for plugins no approval asks for anymore. Otherwise, an
        // organization which rolls the same plugin out again later would find its default silently
        // ignored, because we would still consider it applied:
        //
        var leftOverActivations = appliedActivations.Where(hash => !IsOrganizationDefaultActivation(approvalsByHash, hash)).ToList();
        foreach (var leftOverActivation in leftOverActivations)
        {
            appliedActivations.Remove(leftOverActivation);
            wasConfigurationChanged = true;
        }

        if (leftOverActivations.Count > 0)
            LOG.LogInformation($"Forgot {leftOverActivations.Count} applied organization default(s) for assistant plugin activations, because your organization does not ask for them anymore.");

        return wasConfigurationChanged;
    }

    private static bool IsOrganizationDefaultActivation(Dictionary<string, DataAssistantPluginEnterpriseApproval> approvalsByHash, string pluginHash)
        => approvalsByHash.TryGetValue(pluginHash, out var approval) && approval is { Activate: true, AllowUserOverride: true };

    private static string NormalizeAssistantHash(string hash) => string.IsNullOrWhiteSpace(hash) ? string.Empty : hash.Trim().ToUpperInvariant();
}