using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// Searches through Staan, a European search index reachable with an API key.
/// </summary>
/// <remarks>
/// This is the backend for someone who wants a working web search without running a search
/// instance: an API key is copied into the settings and that is all. In exchange Staan is
/// narrower than a self-hosted instance. It searches one of three markets at a time, filters
/// neither by time nor for explicit results, and serves ten hits per page up to the fourth
/// page.<br/><br/>
/// Only Staan's base search API is used. Its variant for AI agents returns whole pages and
/// costs twice as much, while this tool loads, cleans, and checks the pages itself anyway.
/// </remarks>
public sealed class StaanSearchBackend : IWebSearchBackend
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(StaanSearchBackend).Namespace, nameof(StaanSearchBackend));

    private const string SETTINGS_GROUP = "staan";

    private const string API_KEY_SETTING = $"{SETTINGS_GROUP}.apiKey";

    private const string MARKET_SETTING = $"{SETTINGS_GROUP}.market";

    private const string MARKET_GERMANY = "de-de";

    private const string MARKET_UNITED_STATES = "en-us";

    private const string MARKET_FRANCE = "fr-fr";

    /// <remarks>
    /// Staan itself falls back to the French market. This app is English by default and its
    /// users are anywhere, so the widest index is the better answer to an unset market.
    /// </remarks>
    private const string DEFAULT_MARKET = MARKET_UNITED_STATES;

    private static readonly string[] SUPPORTED_MARKETS = [MARKET_GERMANY, MARKET_UNITED_STATES, MARKET_FRANCE];

    /// <summary>
    /// Staan serves ten hits per page and accepts an offset of at most 30, which is four pages.
    /// </summary>
    private const int RESULTS_PER_PAGE = 10;

    private const int MAX_PAGE = 4;

    private const int MAX_QUERY_CHARACTERS = 400;

    private readonly StaanSearchClient searchClient = new();

    public WebSearchBackend Backend => WebSearchBackend.STAAN;

    public string SettingsGroup => SETTINGS_GROUP;

    /// <remarks>
    /// Staan's search takes a query, a market, and an offset, and nothing else: there is no
    /// safe search parameter and no way to ask for recent results. The market is what restricts
    /// the language, which is the one filter Staan does have — even though it restricts the
    /// region along with it.
    /// </remarks>
    public WebSearchCapabilities Capabilities { get; } = new(SupportsSafeSearch: false, SupportsTimeRange: false, SupportsLanguage: true, MaxPage: MAX_PAGE);

    public void DeclareSettings(ToolSettingsSchemaBuilder builder) => builder
        .InGroup(SETTINGS_GROUP)
        .OptionalSecret(API_KEY_SETTING)
        .OptionalEnum(MARKET_SETTING, SUPPORTED_MARKETS)
        .InGroup(string.Empty);

    public string GetSettingsGroupLabel() => TB("Staan");

    public IReadOnlyList<ToolSettingsGroupLink> GetSettingsGroupLinks() =>
    [
        new(TB("Get an API key"), "https://staan.ai"),
        new(TB("Documentation"), "https://docs.staan.ai/docs/web-search"),
    ];

    public string GetSettingsFieldLabel(string fieldName) => fieldName switch
    {
        API_KEY_SETTING => TB("Staan API Key"),
        MARKET_SETTING => TB("Staan Market"),

        _ => fieldName,
    };

    public string GetSettingsFieldDescription(string fieldName) => fieldName switch
    {
        API_KEY_SETTING => TB("Your Staan API key. It is kept in your operating system's keyring, not in a settings file. Staan is a European search index; the first requests are free of charge, after which searching is billed per thousand requests."),
        MARKET_SETTING => TB("The market Staan searches in. Staan searches one market at a time and offers only these three. When the AI model asks for German, English, or French, the matching market is used no matter what is chosen here; this setting decides what happens for every other language and when no language is requested at all."),

        _ => string.Empty,
    };

    public string? GetSettingsFieldDefaultValue(string fieldName) => fieldName switch
    {
        MARKET_SETTING => DEFAULT_MARKET,

        _ => null,
    };

    public bool IsConfigured(IReadOnlyDictionary<string, string> settingsValues) => !string.IsNullOrWhiteSpace(settingsValues.GetValueOrDefault(API_KEY_SETTING));

    public bool TryValidateConfiguration(IReadOnlyDictionary<string, string> settingsValues, out string error)
    {
        error = string.Empty;

        //
        // The market is picked from a list in the dialog, but a stored value can come from an
        // organization's configuration. Staan answers an unknown market with a rejected
        // request, which would look like a broken API key:
        //
        var market = settingsValues.GetValueOrDefault(MARKET_SETTING);
        if (string.IsNullOrWhiteSpace(market) || IsSupportedMarket(market))
            return true;

        error = string.Format(TB("The configured Staan market '{0}' is not one of the markets Staan offers. Please choose one of these: {1}."), market, string.Join(", ", SUPPORTED_MARKETS));
        return false;
    }

    public async Task<WebSearchBackendResult> SearchAsync(WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token = default)
    {
        var apiKey = settingsValues.GetValueOrDefault(API_KEY_SETTING);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(TB("A Staan API key is required."));

        //
        // Staan refuses a longer query outright. Shortening it here would search for something
        // other than what was asked, so the model is told and can search again instead:
        //
        if (query.Query.Length > MAX_QUERY_CHARACTERS)
            throw new InvalidOperationException($"Staan accepts a search query of at most {MAX_QUERY_CHARACTERS} characters, but this query has {query.Query.Length}. Search again with a shorter query.");

        //
        // That Staan cannot restrict a search to a period of time is reported by the tool from
        // this backend's capabilities, so it is not repeated here. What stays here is the market,
        // because no capability flag can express which language Staan searched instead.
        //
        var notes = new List<string>();
        var market = ResolveMarket(query.Language, settingsValues, notes);

        var searchRequest = new StaanSearchRequest { Query = query.Query, Market = market, Offset = ReadOffset(query.Page) };
        var response = await this.searchClient.SearchAsync(apiKey.Trim(), searchRequest, query.TimeoutSeconds, token);

        //
        // Staan corrects an obvious mistake in a query and says so. Which query actually ran is
        // the difference between nothing matching the question and something else having been
        // asked, and only the correction explains a result set that does not fit the question:
        //
        var alteredQuery = response.Query?.AlteredQuery;
        if (!string.IsNullOrWhiteSpace(alteredQuery) && !string.Equals(alteredQuery, query.Query, StringComparison.Ordinal))
            notes.Add($"Staan corrected the query and searched for '{alteredQuery}' instead.");

        var hits = (response.Web?.Results ?? []).Select(result => new SearchHit(result.Url, result.Title, result.Snippet));
        var candidates = SearchCandidateCollector.Collect(WebSearchBackend.STAAN, hits, query.Limit, out var candidateCount);
        return new WebSearchBackendResult(WebSearchBackend.STAAN, candidates, candidateCount, notes);
    }

    /// <summary>
    /// The market to search in, from the language the tool asked for.
    /// </summary>
    /// <remarks>
    /// Staan searches one market at a time, so a language it does not offer cannot simply be
    /// dropped the way an optional filter could: the search runs in some market either way, and
    /// results would arrive in another language than the one that was asked for. Saying so is
    /// what the note is for.
    /// </remarks>
    private static string ResolveMarket(string? language, IReadOnlyDictionary<string, string> settingsValues, List<string> notes)
    {
        var configuredMarket = settingsValues.GetValueOrDefault(MARKET_SETTING);
        var fallbackMarket = IsSupportedMarket(configuredMarket) ? configuredMarket!.Trim().ToLowerInvariant() : DEFAULT_MARKET;
        if (string.IsNullOrWhiteSpace(language) || string.Equals(language, ToolSettingsOptionSources.ANY_LANGUAGE, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Staan always searches one market and cannot search all of them at once, so it searched the '{fallbackMarket}' market.");
            return fallbackMarket;
        }

        var market = MapLanguageToMarket(language);
        if (market is not null)
            return market;

        notes.Add($"Staan offers no market for the language '{language}', so it searched the '{fallbackMarket}' market instead. The results are therefore not in the requested language.");
        return fallbackMarket;
    }

    /// <remarks>
    /// Matched on the primary subtag, so that Austrian German reaches the German market and
    /// British English the English one. A market is a region as much as a language, so this
    /// trades the region away to keep the language.
    /// </remarks>
    private static string? MapLanguageToMarket(string language) => language.Split('-')[0].ToLowerInvariant() switch
    {
        "de" => MARKET_GERMANY,
        "en" => MARKET_UNITED_STATES,
        "fr" => MARKET_FRANCE,

        _ => null,
    };

    /// <summary>
    /// The offset addressing one result page.
    /// </summary>
    /// <remarks>
    /// Staan pages by offset instead of by page number, in steps of its fixed page size. The
    /// tool keeps the requested page within the maximum this backend reports, so nothing needs
    /// clamping here.
    /// </remarks>
    private static int? ReadOffset(int? page) => page is null or <= 1 ? null : (page.Value - 1) * RESULTS_PER_PAGE;

    private static bool IsSupportedMarket(string? market) => !string.IsNullOrWhiteSpace(market) && SUPPORTED_MARKETS.Contains(market.Trim(), StringComparer.OrdinalIgnoreCase);
}