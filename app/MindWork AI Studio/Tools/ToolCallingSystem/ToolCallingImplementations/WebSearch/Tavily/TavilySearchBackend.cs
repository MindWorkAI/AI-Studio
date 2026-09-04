using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

/// <summary>
/// Searches through Tavily, a search service built for AI agents.
/// </summary>
/// <remarks>
/// This is the backend that asks the least of a user: an account without a credit card, a key
/// copied into the settings, and a thousand searches a month. Tavily can filter by language,
/// by time, and for explicit results, so nothing of a search has to be dropped. What it does
/// not offer is paging: it answers one result list per search and nothing beyond it.
/// </remarks>
public sealed class TavilySearchBackend : IWebSearchBackend
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(TavilySearchBackend).Namespace, nameof(TavilySearchBackend));

    private const string SETTINGS_GROUP = "tavily";

    private const string API_KEY_SETTING = $"{SETTINGS_GROUP}.apiKey";

    private const string SEARCH_DEPTH_SETTING = $"{SETTINGS_GROUP}.searchDepth";

    private const string SEARCH_DEPTH_BASIC = "basic";

    private const string SEARCH_DEPTH_ADVANCED = "advanced";

    private const string DEFAULT_SEARCH_DEPTH = SEARCH_DEPTH_BASIC;

    /// <remarks>
    /// Tavily knows two faster depths as well, and both silently drop the safe search parameter.
    /// A search that quietly ignores a filtering policy is worse than a slower search, so they
    /// are not offered.
    /// </remarks>
    private static readonly string[] SUPPORTED_SEARCH_DEPTHS = [SEARCH_DEPTH_BASIC, SEARCH_DEPTH_ADVANCED];

    private const int MAX_RESULTS = 20;

    /// <summary>
    /// Tavily answers one result list per search and offers no way to ask for the next one.
    /// </summary>
    private const int MAX_PAGE = 1;

    private readonly TavilySearchClient searchClient = new();

    public WebSearchBackend Backend => WebSearchBackend.TAVILY;

    public string SettingsGroup => SETTINGS_GROUP;

    public int MaxPage => MAX_PAGE;

    public void DeclareSettings(ToolSettingsSchemaBuilder builder) => builder
        .InGroup(SETTINGS_GROUP)
        .OptionalSecret(API_KEY_SETTING)
        .OptionalEnum(SEARCH_DEPTH_SETTING, SUPPORTED_SEARCH_DEPTHS)
        .InGroup(string.Empty);

    public string GetSettingsGroupLabel() => TB("Tavily");

    public IReadOnlyList<ToolSettingsGroupLink> GetSettingsGroupLinks() =>
    [
        new(TB("Create account"), "https://app.tavily.com"),
        new(TB("Usage and billing"), "https://app.tavily.com/billing"),
    ];

    public string GetSettingsFieldLabel(string fieldName) => fieldName switch
    {
        API_KEY_SETTING => TB("Tavily API Key"),
        SEARCH_DEPTH_SETTING => TB("Tavily Search Depth"),

        _ => fieldName,
    };

    public string GetSettingsFieldDescription(string fieldName) => fieldName switch
    {
        API_KEY_SETTING => TB("Your Tavily API key. It is kept in your operating system's keyring, not in a settings file. Tavily grants 1,000 requests per month without a credit card, which is enough for everyday use."),
        SEARCH_DEPTH_SETTING => TB("How thoroughly Tavily searches. A basic search costs one of your monthly requests, an advanced search costs two and looks at more of each page before deciding how well it matches. Basic is the sensible choice unless you notice that results are missing the point."),

        _ => string.Empty,
    };

    public string? GetSettingsFieldDefaultValue(string fieldName) => fieldName switch
    {
        SEARCH_DEPTH_SETTING => DEFAULT_SEARCH_DEPTH,

        _ => null,
    };

    public bool IsConfigured(IReadOnlyDictionary<string, string> settingsValues) => !string.IsNullOrWhiteSpace(settingsValues.GetValueOrDefault(API_KEY_SETTING));

    public bool TryValidateConfiguration(IReadOnlyDictionary<string, string> settingsValues, out string error)
    {
        error = string.Empty;

        //
        // The depth is picked from a list in the dialog, but a stored value can come from an
        // organization's configuration. One of Tavily's faster depths would be accepted by the
        // API and would then ignore the safe search policy without saying so:
        //
        var searchDepth = settingsValues.GetValueOrDefault(SEARCH_DEPTH_SETTING);
        if (string.IsNullOrWhiteSpace(searchDepth) || IsSupportedSearchDepth(searchDepth))
            return true;

        error = string.Format(TB("The configured Tavily search depth '{0}' is not one this app supports. Please choose one of these: {1}."), searchDepth, string.Join(", ", SUPPORTED_SEARCH_DEPTHS));
        return false;
    }

    public async Task<WebSearchBackendResult> SearchAsync(WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token = default)
    {
        var apiKey = settingsValues.GetValueOrDefault(API_KEY_SETTING);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(TB("A Tavily API key is required."));

        var notes = new List<string>();
        var language = ResolveLanguage(query.Language, notes);
        var searchRequest = new TavilySearchRequest
        {
            Query = query.Query,
            SearchDepth = ResolveSearchDepth(settingsValues),
            MaxResults = Math.Min(query.Limit, MAX_RESULTS),
            TimeRange = string.IsNullOrWhiteSpace(query.TimeRange) ? null : query.TimeRange,
            Language = language,

            //
            // Without this, Tavily treats the language as a preference and still returns pages
            // in other languages. The tool promises to restrict the search, and the other
            // backends do restrict, so a language asked for here is a requirement. Anyone who
            // would rather have more hits than one language can choose any language instead.
            //
            FilterByLanguage = language is null ? null : true,
            SafeSearch = query.SafeSearch?.ToTavilyValue(),
        };

        var response = await this.searchClient.SearchAsync(apiKey.Trim(), searchRequest, query.TimeoutSeconds, token);
        var hits = response.Results.Select(result => new SearchHit(result.Url, result.Title, result.Content));
        var candidates = SearchCandidateCollector.Collect(WebSearchBackend.TAVILY, hits, query.Limit, out var candidateCount);
        return new WebSearchBackendResult(WebSearchBackend.TAVILY, candidates, candidateCount, notes);
    }

    /// <summary>
    /// The language code to send, from the language tag the tool asked for.
    /// </summary>
    /// <remarks>
    /// Tavily expects the language alone, so the region of a tag is dropped: Austrian German
    /// searches as German. A tag whose language part is not one of the two-letter codes cannot
    /// be translated, and Tavily would reject it, so the search runs unrestricted and says so.
    /// </remarks>
    private static string? ResolveLanguage(string? language, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(language) || string.Equals(language, ToolSettingsOptionSources.ANY_LANGUAGE, StringComparison.OrdinalIgnoreCase))
            return null;

        var languageCode = language.Split('-')[0].Trim().ToLowerInvariant();
        if (languageCode.Length is 2 && languageCode.All(char.IsAsciiLetterLower))
            return languageCode;

        notes.Add($"Tavily could not read '{language}' as a language, so it searched without restricting the language of the results.");
        return null;
    }

    /// <remarks>
    /// Always sent rather than left out, so that a change of Tavily's own default cannot change
    /// what a search costs here. It also decides whether the safe search policy is honoured at
    /// all, which is reason enough not to leave it to the other side.
    /// </remarks>
    private static string ResolveSearchDepth(IReadOnlyDictionary<string, string> settingsValues)
    {
        var configuredSearchDepth = settingsValues.GetValueOrDefault(SEARCH_DEPTH_SETTING);
        return IsSupportedSearchDepth(configuredSearchDepth) ? configuredSearchDepth!.Trim().ToLowerInvariant() : DEFAULT_SEARCH_DEPTH;
    }

    private static bool IsSupportedSearchDepth(string? searchDepth) => !string.IsNullOrWhiteSpace(searchDepth) && SUPPORTED_SEARCH_DEPTHS.Contains(searchDepth.Trim(), StringComparer.OrdinalIgnoreCase);
}