using System.Linq.Expressions;
using AIStudio.Agents.AssistantAudit;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Settings for auditing assistant plugins before activation.
/// </summary>
public sealed class DataAssistantPluginAudit(Expression<Func<Data, DataAssistantPluginAudit>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataAssistantPluginAudit() : this(null)
    {
    }
    
    /// <summary>
    /// Should assistant plugins be audited before they can be activated?
    /// </summary>
    public bool RequireAuditBeforeActivation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.RequireAuditBeforeActivation, true);

    /// <summary>
    /// Which provider should be used for the assistant plugin audit?
    /// When empty, the app-wide default provider is used.
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedAgentProvider, string.Empty);

    /// <summary>
    /// The minimum audit level assistant plugins should meet.
    /// </summary>
    public AssistantAuditLevel MinimumLevel { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumLevel, AssistantAuditLevel.CAUTION);

    /// <summary>
    /// Should activation be blocked when the audit result is below the minimum level?
    /// </summary>
    public bool BlockActivationBelowMinimum { get; set; } = ManagedConfiguration.Register(configSelection, n => n.BlockActivationBelowMinimum, true);
    
    /// <summary>
    /// If true, the security audit will be hidden from the user and done in the background
    /// </summary>
    public bool AutomaticallyAuditAssistants { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AutomaticallyAuditAssistants, false);
}
