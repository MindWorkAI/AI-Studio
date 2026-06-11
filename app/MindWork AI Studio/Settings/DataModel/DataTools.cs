using System.Linq.Expressions;

using AIStudio.Settings;

namespace AIStudio.Settings.DataModel;

public sealed class DataTools(Expression<Func<Data, DataTools>>? configSelection = null)
{
    public DataTools() : this(null)
    {
    }

    public Dictionary<string, Dictionary<string, string>> Settings { get; set; } = [];

    public Dictionary<string, HashSet<string>> DefaultToolIdsByComponent { get; set; } = [];

    public HashSet<string> VisibleToolSelectionComponents { get; set; } = [];

    public Dictionary<string, string> MinimumProviderConfidenceByToolId { get; set; } = ManagedConfiguration.Register<DataTools, Dictionary<string, string>>(
        configSelection,
        x => x.MinimumProviderConfidenceByToolId,
        new Dictionary<string, string>(StringComparer.Ordinal));

    public string WebSearchBaseUrl { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchBaseUrl,
        string.Empty);

    public string ReadWebPageAllowedPrivateHosts { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.ReadWebPageAllowedPrivateHosts,
        string.Empty);
}
