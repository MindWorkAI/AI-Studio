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

    public Dictionary<string, HashSet<string>> DefaultToolIdsByComponent { get; set; } = [];

    public HashSet<string> VisibleToolSelectionComponents { get; set; } = [];

    public bool EnableTools { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.EnableTools,
        true);

    public HashSet<string> DisabledToolIds { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.DisabledToolIds,
        []);

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
    /// Secrets never travel this way. They belong in the operating system's keyring, which a
    /// configuration file cannot reach.
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
