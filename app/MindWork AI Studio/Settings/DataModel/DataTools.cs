using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

public sealed class DataTools(Expression<Func<Data, DataTools>>? configSelection = null)
{
    public DataTools() : this(null)
    {
    }

    /// <summary>
    /// The settings the user entered per tool: tool ID, then field name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Settings { get; set; } = [];

    /// <summary>
    /// The tool collections preselected per component, see DisabledToolIds for the IDs.
    /// </summary>
    public Dictionary<string, HashSet<string>> DefaultToolIdsByComponent { get; set; } = [];

    public HashSet<string> VisibleToolSelectionComponents { get; set; } = [];

    public bool EnableTools { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.EnableTools,
        true);

    /// <summary>
    /// The tool collections an organization switched off.
    /// </summary>
    /// <remarks>
    /// The IDs are those of tool collections. A tool outside of a declared collection forms one of
    /// its own under its own ID, which is why the names of this and the other settings still speak
    /// of tools. The ID of a tool inside a declared collection stands for its whole collection, see
    /// ToolRegistry.GetCollectionId.
    /// </remarks>
    public HashSet<string> DisabledToolIds { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.DisabledToolIds,
        []);

    /// <summary>
    /// The minimum provider confidence per tool collection, see DisabledToolIds for the IDs.
    /// </summary>
    /// <remarks>
    /// When the ID of a collection and those of its tools carry different levels, the highest one
    /// applies, see SettingsManager.GetMinimumProviderConfidenceResolution.
    /// </remarks>
    public Dictionary<string, string> MinimumProviderConfidenceByToolId { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.MinimumProviderConfidenceByToolId,
        new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Tool settings an organization fixed, which the user cannot change. Keys are
    /// "toolId.fieldName".
    /// </summary>
    /// <remarks>
    /// Keyed by tool and field rather than held in a property per setting, because a property per
    /// setting only works for the tools AI Studio ships. Tools defined by plugin authors are not
    /// known at compile time, yet an organization has to be able to configure them the same way.
    /// <br/><br/>
    /// A secret field travels here too, but only encrypted with the enterprise secret, in the
    /// same "ENC:v1:" form the providers use for their API keys. What is stored is therefore
    /// ciphertext, worthless without a secret that lives outside every deployed file. A plaintext
    /// secret is refused rather than used, and a secret is never accepted as a pre-filled default
    /// — see the tool settings service for both rules.
    /// </remarks>
    public Dictionary<string, string> LockedToolSettings { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.LockedToolSettings,
        new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Tool settings an organization pre-filled but left changeable. Keys are "toolId.fieldName".
    /// </summary>
    /// <remarks>
    /// Applies until the user saves a value of their own, which then wins. That is the difference
    /// to the locked settings above, and the reason both exist: an organization can fix the search
    /// instance while leaving the timeouts to the user.
    /// </remarks>
    public Dictionary<string, string> DefaultToolSettings { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.DefaultToolSettings,
        new Dictionary<string, string>(StringComparer.Ordinal));
}
