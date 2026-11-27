using AIStudio.AIOperations.PluginAudit;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings for auditing assistant plugins, as settings files up to version V6 store them.
/// </summary>
public sealed class DataAssistantPluginAuditV6
{
    /// <summary>
    /// Should assistant plugins be audited before they can be activated?
    /// </summary>
    public bool RequireAuditBeforeActivation { get; set; } = true;

    /// <summary>
    /// Which provider should be used for the assistant plugin audit?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level of the provider which audits the plugins.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The minimum audit level assistant plugins should meet.
    /// </summary>
    public PluginAuditLevel MinimumLevel { get; set; } = PluginAuditLevel.CAUTION;

    /// <summary>
    /// Should activation be blocked when the audit result is below the minimum level?
    /// </summary>
    public bool BlockActivationBelowMinimum { get; set; } = true;

    /// <summary>
    /// If true, the security audit will be hidden from the user and done in the background.
    /// </summary>
    public bool AutomaticallyAuditAssistants { get; set; }

    /// <summary>
    /// Enterprise-managed assistant plugin hashes that are approved without requiring a user audit.
    /// </summary>
    public IList<DataAssistantPluginEnterpriseApproval> EnterpriseApprovedPlugins { get; set; } = [];
}