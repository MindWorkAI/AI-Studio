using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a research agent of the BeyondFacts Lab found on the web about one topic, stored as research/&lt;topic&gt;.json
/// in the folder of the step "Strategy".
/// </summary>
/// <remarks>
/// Besides the text, a research keeps what left AI Studio for it: the public details it received, the queries it sent
/// to the web search, and the addresses of the pages it opened. The person sees all of it, so that they can check that
/// nothing else of their preparation went out. Whether the research is outdated follows from the details it received,
/// see IsOutdated.
/// </remarks>
public sealed class BflResearch
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// How many characters of the answer of the agent a research keeps at most, since the text goes into the prompt of
    /// the step "Strategy".
    /// </summary>
    public const int MAX_TEXT_CHARACTERS = 20_000;

    /// <summary>
    /// How many characters of the title of a page a research keeps at most.
    /// </summary>
    public const int MAX_TITLE_CHARACTERS = 300;

    /// <summary>
    /// The argument with which the tool Web Search gets its query, see WebSearchTool.
    /// </summary>
    internal const string QUERY_ARGUMENT = "query";

    /// <summary>
    /// The argument with which the tool Read Web Page gets its address, see ReadWebPageTool.
    /// </summary>
    internal const string URL_ARGUMENT = "url";

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets what the agent found, in Markdown, with the sources as links.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public details the agent received, in the order of the fields.
    /// </summary>
    public List<BflResearchDetail> Details { get; set; } = [];

    /// <summary>
    /// Gets or sets the queries the agent sent to the web search, in the order it sent them.
    /// </summary>
    public List<string> SearchQueries { get; set; } = [];

    /// <summary>
    /// Gets or sets the pages the agent opened, in the order it opened them.
    /// </summary>
    public List<BflResearchPage> Pages { get; set; } = [];

    /// <summary>
    /// Gets or sets when the agent finished.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Tells whether the public details changed since the agent received them, so that the research may no longer fit
    /// the event.
    /// </summary>
    /// <param name="details">The public details as they are now, see BflPromptComposer.PublicDetailsOf.</param>
    public bool IsOutdated(IReadOnlyList<BflResearchDetail> details) => this.Details.Count != details.Count || this.Details.Zip(details).Any(pair => !string.Equals(pair.First.FieldId, pair.Second.FieldId, StringComparison.Ordinal) || !string.Equals(pair.First.Value, pair.Second.Value, StringComparison.Ordinal));

    /// <summary>
    /// Reads a research from the answer of an agent and the calls of its tools.
    /// </summary>
    /// <remarks>
    /// A query or an address counts once the tool ran with it, whether it found something or not: then it has left AI
    /// Studio. A call which the lab or the organization blocked sent nothing. The text is limited, since it goes into
    /// the prompt of the step "Strategy"; it is the answer of a model which read only filtered web content.
    /// </remarks>
    /// <param name="answer">The answer of the agent.</param>
    /// <param name="invocations">The calls of tools the agent made.</param>
    /// <param name="sources">The sources the tools reported, for the titles of the pages.</param>
    /// <param name="details">The public details the agent received.</param>
    /// <param name="createdAtUtc">When the agent finished.</param>
    /// <returns>The research; its text is empty when the agent wrote nothing.</returns>
    public static BflResearch Of(string answer, IEnumerable<ToolInvocationTrace> invocations, IEnumerable<Source> sources, IReadOnlyList<BflResearchDetail> details, DateTimeOffset createdAtUtc)
    {
        var text = BflResultText.Read(answer);
        if (text.Length > MAX_TEXT_CHARACTERS)
            text = BflMaterialBudget.EndAtLineEnd(text[..MAX_TEXT_CHARACTERS]);

        var executed = invocations.Where(invocation => invocation.WasExecuted).OrderBy(invocation => invocation.Order).ToList();
        var queries = executed
            .Where(invocation => string.Equals(invocation.ToolId, ToolSelectionRules.WEB_SEARCH_TOOL_ID, StringComparison.Ordinal))
            .Select(invocation => invocation.Arguments.GetValueOrDefault(QUERY_ARGUMENT, string.Empty).Trim())
            .Where(query => query.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources.Where(source => source.Origin is SourceOrigin.TOOL && !string.IsNullOrWhiteSpace(source.Title)))
            titles.TryAdd(source.URL, source.Title.Trim());

        var pages = executed
            .Where(invocation => string.Equals(invocation.ToolId, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, StringComparison.Ordinal))
            .Select(invocation => invocation.Arguments.GetValueOrDefault(URL_ARGUMENT, string.Empty).Trim())
            .Where(IsWebAddress)
            .Distinct(StringComparer.Ordinal)
            .Select(url => new BflResearchPage { Url = url, Title = Shorten(titles.GetValueOrDefault(url, url)) })
            .ToList();

        return new BflResearch
        {
            Text = text,
            Details = [.. details.Select(detail => new BflResearchDetail { FieldId = detail.FieldId, Value = detail.Value })],
            SearchQueries = queries,
            Pages = pages,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>
    /// Tells whether a text is a complete address of a web page.
    /// </summary>
    /// <param name="url">The text.</param>
    private static bool IsWebAddress(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// Limits the title of a page.
    /// </summary>
    /// <param name="title">The title.</param>
    private static string Shorten(string title) => title.Length <= MAX_TITLE_CHARACTERS ? title : $"{title[..(MAX_TITLE_CHARACTERS - 1)].TrimEnd()}…";
}