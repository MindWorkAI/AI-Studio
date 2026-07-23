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

    public bool EnableTools { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.EnableTools,
        true);

    public HashSet<string> DisabledToolIds { get; set; } = ManagedConfiguration.Register(
        configSelection,
        x => x.DisabledToolIds,
        []);

    public Dictionary<string, string> MinimumProviderConfidenceByToolId { get; set; } = ManagedConfiguration.Register<DataTools, Dictionary<string, string>>(
        configSelection,
        x => x.MinimumProviderConfidenceByToolId,
        new Dictionary<string, string>(StringComparer.Ordinal));

    public string WebSearchBaseUrl { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchBaseUrl,
        string.Empty);

    public string WebSearchDefaultLanguage { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchDefaultLanguage,
        string.Empty);

    public string WebSearchDefaultSafeSearch { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchDefaultSafeSearch,
        string.Empty);

    public string WebSearchMaxResults { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchMaxResults,
        string.Empty);

    public string WebSearchTimeoutSeconds { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchTimeoutSeconds,
        string.Empty);

    public string WebSearchMaxTotalContentCharacters { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchMaxTotalContentCharacters,
        string.Empty);

    public string WebSearchMinContentCharactersPerResult { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchMinContentCharactersPerResult,
        string.Empty);

    public string WebSearchPageTimeoutSeconds { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchPageTimeoutSeconds,
        string.Empty);

    public string WebSearchRetrievalTimeoutSeconds { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.WebSearchRetrievalTimeoutSeconds,
        string.Empty);

    public string ReadWebPageTimeoutSeconds { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.ReadWebPageTimeoutSeconds,
        string.Empty);

    public string ReadWebPageMaxContentCharacters { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.ReadWebPageMaxContentCharacters,
        string.Empty);

    public string ReadWebPageAllowedPrivateHosts { get; set; } = ManagedConfiguration.Register<DataTools>(
        configSelection,
        x => x.ReadWebPageAllowedPrivateHosts,
        string.Empty);
}
