using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.SearXNG;

/// <summary>
/// Searches through a SearXNG instance the user or their organization runs.
/// </summary>
/// <remarks>
/// The instance decides which engines it asks and how, so this backend sends no engine or
/// category parameters. What it does need is an instance that serves the JSON format, which
/// is why the base URL is the one thing it asks the user for.
/// </remarks>
public sealed class SearXNGSearchBackend : IWebSearchBackend
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SearXNGSearchBackend).Namespace, nameof(SearXNGSearchBackend));

    private const string SETTINGS_GROUP = "searxng";

    private const string BASE_URL_SETTING = $"{SETTINGS_GROUP}.baseUrl";

    private const int MAX_PAGE = 20;

    private readonly SearXNGSearchClient searchClient = new();

    public WebSearchBackend Backend => WebSearchBackend.SEARXNG;

    public string SettingsGroup => SETTINGS_GROUP;

    /// <remarks>
    /// An instance passes every filter on to the engines it asks, so all of them are on offer
    /// here. How faithfully a single engine honours one of them is that engine's business, and
    /// an instance already reports the engines that did not answer at all.
    /// </remarks>
    public WebSearchCapabilities Capabilities { get; } = new(SupportsSafeSearch: true, SupportsTimeRange: true, SupportsLanguage: true, MaxPage: MAX_PAGE);

    public void DeclareSettings(ToolSettingsSchemaBuilder builder) => builder
        .InGroup(SETTINGS_GROUP)
        .Optional(BASE_URL_SETTING)
        .InGroup(string.Empty);

    public string GetSettingsGroupLabel() => TB("SearXNG instance");

    //
    // The search settings rather than the documentation's front page: that is where an
    // instance's result formats are listed, and whether 'json' is among them decides whether
    // this backend can talk to the instance at all. It is the most common reason a freshly
    // set up instance answers nothing.
    //
    public IReadOnlyList<ToolSettingsGroupLink> GetSettingsGroupLinks() =>
    [
        new(TB("Documentation"), "https://docs.searxng.org/admin/settings/settings_search.html"),
    ];

    public string GetSettingsFieldLabel(string fieldName) => fieldName switch
    {
        BASE_URL_SETTING => TB("SearXNG URL"),
        _ => fieldName,
    };

    public string GetSettingsFieldDescription(string fieldName) => fieldName switch
    {
        BASE_URL_SETTING => TB("Base URL of the SearXNG instance. You can enter either the instance root URL or the /search endpoint. The instance must have the JSON format enabled, which means 'json' has to be listed under 'search.formats' in its settings.yml. Public instances usually serve only the web interface and additionally block automated requests, so a self-hosted instance is the reliable option."),
        _ => string.Empty,
    };

    public string? GetSettingsFieldDefaultValue(string fieldName) => null;

    public bool IsConfigured(IReadOnlyDictionary<string, string> settingsValues) => !string.IsNullOrWhiteSpace(settingsValues.GetValueOrDefault(BASE_URL_SETTING));

    public bool TryValidateConfiguration(IReadOnlyDictionary<string, string> settingsValues, out string error) => TryReadSearchUri(settingsValues, out _, out error);

    public async Task<WebSearchBackendResult> SearchAsync(WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token = default)
    {
        if (!TryReadSearchUri(settingsValues, out var searchUri, out var uriError))
            throw new InvalidOperationException(uriError);

        // No configured policy sends nothing at all, which leaves the decision to the
        // instance's own configuration:
        var safeSearch = query.SafeSearch?.ToSearXNGValue();
        var response = await this.searchClient.SearchAsync(new SearXNGSearchRequest(searchUri, query.Query, query.Language, query.TimeRange, query.Page, safeSearch, query.Limit, query.TimeoutSeconds), token);

        //
        // Which engines did not answer is the difference between "nothing matches this query"
        // and "this instance has no working engines", which is the usual state of a fresh
        // instance whose engines answer with a CAPTCHA or time out. Without it, a
        // misconfigured instance is indistinguishable from an obscure query.
        //
        IReadOnlyList<string> notes = response.UnresponsiveEngines.Count is 0
            ? []
            : [$"The following search engines of the SearXNG instance did not answer: {string.Join(", ", response.UnresponsiveEngines)}."];

        return new WebSearchBackendResult(WebSearchBackend.SEARXNG, response.Candidates, response.CandidateCount, notes);
    }

    private static bool TryReadSearchUri(IReadOnlyDictionary<string, string> settingsValues, out Uri searchUri, out string error) =>
        SearXNGSearchClient.TryNormalizeSearchUri(
            settingsValues.GetValueOrDefault(BASE_URL_SETTING) ?? string.Empty,
            TB("A SearXNG URL is required."),
            TB("The configured SearXNG URL is not a valid absolute URL."),
            TB("The configured SearXNG URL must start with http:// or https://."),
            out searchUri,
            out error);
}