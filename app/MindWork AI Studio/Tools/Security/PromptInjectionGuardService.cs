using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Security;

/// <summary>
/// Filters prompt injections out of external content before it reaches a model.
/// </summary>
/// <remarks>
/// The detection itself lives in the Rust runtime. File content is filtered while the runtime
/// streams it, so it never passes through here; what this service adds is the path for content
/// the runtime does not read itself — web pages and retrieval contexts — and the reporting the
/// user sees.
/// </remarks>
public sealed class PromptInjectionGuardService(
    RustService rustService,
    SettingsManager settingsManager,
    ILogger<PromptInjectionGuardService> logger,
    ILoggerFactory loggerFactory)
{
    public const string WIKI_URL = "https://en.wikipedia.org/wiki/Prompt_engineering#Prompt_injection";

    private const string DETECTION_LOG_CATEGORY = "PromptInjectionProtection";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PromptInjectionGuardService).Namespace, nameof(PromptInjectionGuardService));

    private readonly ILogger detectionLogger = loggerFactory.CreateLogger(DETECTION_LOG_CATEGORY);
    private readonly Lock reportLock = new();
    private readonly List<PromptInjectionScanResult> pendingResults = [];
    private int openActions;

    /// <summary>
    /// Filters prompt injections out of a text the runtime did not read itself, such as a web
    /// page or a retrieval context.
    /// </summary>
    /// <remarks>
    /// Returns usable text in every case. When the runtime cannot be reached, the text is passed
    /// through unchanged: refusing the user's content because a check could not run would cost
    /// them their work over a check that is best-effort anyway. The failure is logged and shown,
    /// so it does not pass silently.
    /// </remarks>
    /// <param name="text">The content to filter.</param>
    /// <param name="source">Where the content came from, for the report shown to the user.</param>
    /// <returns>The content with any suspicious passages removed.</returns>
    public async Task<string> SanitizeAsync(string text, PromptInjectionSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (await rustService.SanitizePromptInjections(text) is not { } response)
        {
            logger.LogError("Could not check {SourceKind} '{SourceLabel}' for prompt injections. The content is used unchanged.", source.Kind, source.Label);
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.GppMaybe,
                string.Format(TB("AI Studio could not check '{0}' for prompt injections. The content is used as it is."), source.NotificationLabel)));

            return text;
        }

        if (response.RedactedCount > 0)
            await this.ReportAsync(new(source, response.Findings, response.RedactedCount));

        return response.SanitizedText;
    }

    /// <summary>
    /// Filters prompt injections out of several texts in one runtime request.
    /// </summary>
    /// <remarks>
    /// For content that belongs to one user action, such as every page a web search returned.
    /// The user gets a single report for the whole action, and texts sharing a source are
    /// reported as that one source.<br/><br/>
    /// Returns usable text in every case, for the reason given on the single-text overload. When
    /// the check cannot run, every text is passed through unchanged.
    /// </remarks>
    /// <param name="texts">The contents to filter, each with its source.</param>
    /// <returns>The contents with any suspicious passages removed, in the order they came in.</returns>
    public async Task<IReadOnlyList<string>> SanitizeAsync(IReadOnlyList<PromptInjectionText> texts)
    {
        if (texts.Count is 0)
            return [];

        //
        // Empty fields are common — many pages have no description or authors — and the runtime
        // has nothing to do with them. Only the texts with content are sent, and their positions
        // are remembered so the answer can be put back in the caller's order.
        //
        var sanitizedTexts = texts.Select(x => x.Text).ToArray();
        List<int> indicesToScan = [];
        for (var index = 0; index < texts.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(texts[index].Text))
                indicesToScan.Add(index);
        }

        if (indicesToScan.Count is 0)
            return sanitizedTexts;

        var responses = await rustService.SanitizePromptInjectionsBatch(indicesToScan.Select(index => texts[index].Text).ToList());
        if (responses is null)
        {
            var sources = texts.Select(x => x.Source).Distinct().ToList();
            logger.LogError("Could not check {SourceCount} content source(s) for prompt injections. The content is used unchanged. Sources: {SourceLabels}", sources.Count, string.Join(", ", sources.Select(x => $"{x.Kind} '{x.Label}'")));
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.GppMaybe,
                sources.Count is 1
                    ? string.Format(TB("AI Studio could not check '{0}' for prompt injections. The content is used as it is."), sources[0].NotificationLabel)
                    : string.Format(TB("AI Studio could not check {0} sources for prompt injections. The content is used as it is."), sources.Count)));

            return sanitizedTexts;
        }

        //
        // Findings are collected per source, not per text: a page whose content and title were
        // both filtered is one thing that happened to the user, not two.
        //
        var findingsBySource = new Dictionary<PromptInjectionSource, (List<PromptInjectionFinding> Findings, int RedactedCount)>();
        for (var responseIndex = 0; responseIndex < indicesToScan.Count; responseIndex++)
        {
            var response = responses[responseIndex];
            var textIndex = indicesToScan[responseIndex];
            sanitizedTexts[textIndex] = response.SanitizedText;
            if (response.RedactedCount is 0)
                continue;

            var source = texts[textIndex].Source;
            if (!findingsBySource.TryGetValue(source, out var aggregate))
                aggregate = ([], 0);

            aggregate.Findings.AddRange(response.Findings);
            findingsBySource[source] = (aggregate.Findings, aggregate.RedactedCount + response.RedactedCount);
        }

        if (findingsBySource.Count is 0)
            return sanitizedTexts;

        //
        // One scope around all sources, so a search across five pages reports once instead of
        // five times:
        //
        await using var reportingScope = this.BeginAction();
        foreach (var (source, aggregate) in findingsBySource)
            await this.ReportAsync(new(source, aggregate.Findings, aggregate.RedactedCount));

        return sanitizedTexts;
    }

    /// <summary>
    /// Records what was filtered out of one piece of content and tells the user about it.
    /// </summary>
    /// <remarks>
    /// Within a BeginAction scope the result is collected and reported together
    /// with the rest of that action. Outside of one it is reported immediately: a result that
    /// simply waited for the next scope would either never reach the user, or reach them as
    /// part of an unrelated action later on.
    /// </remarks>
    public async Task ReportAsync(PromptInjectionScanResult result)
    {
        if (!result.WasFiltered)
            return;

        bool reportNow;
        lock (this.reportLock)
        {
            this.pendingResults.Add(result);
            reportNow = this.openActions is 0;
        }

        if (reportNow)
            await this.ReportPendingAsync();
    }

    /// <summary>
    /// Marks the start of one user action, such as attaching a batch of files or sending a
    /// message.
    /// </summary>
    /// <remarks>
    /// Results are collected until the action finishes, so the user gets one report about
    /// twenty documents instead of twenty reports. Actions may nest: only the outermost one
    /// reports.
    /// </remarks>
    /// <returns>A scope that reports what was filtered once it is disposed.</returns>
    public ReportingScope BeginAction()
    {
        lock (this.reportLock)
            this.openActions++;

        return new(this);
    }

    private async Task EndActionAsync()
    {
        lock (this.reportLock)
        {
            this.openActions--;

            // An inner scope reports nothing: the action the user started is still running.
            if (this.openActions > 0)
                return;
        }

        await this.ReportPendingAsync();
    }

    private async Task ReportPendingAsync()
    {
        List<PromptInjectionScanResult> results;
        lock (this.reportLock)
        {
            if (this.pendingResults.Count is 0)
                return;

            results = [..this.pendingResults];
            this.pendingResults.Clear();
        }

        var totalCount = results.Sum(result => result.RedactedCount);
        this.detectionLogger.LogWarning(
            "Detected and removed {PassageCount} potentially dangerous passage(s) in {SourceCount} content source(s).",
            totalCount,
            results.Count);

        await MessageBus.INSTANCE.SendWarning(new(
            Icons.Material.Filled.GppMaybe,
            results.Count is 1
                ? string.Format(TB("AI Studio removed suspicious instructions from '{0}' before using it."), results[0].Source.NotificationLabel)
                : string.Format(TB("AI Studio removed suspicious instructions from {0} sources before using them."), results.Count)));

        if (settingsManager.ConfigurationData.App.ShowPromptInjectionAlert)
            await MessageBus.INSTANCE.SendMessage<PromptInjectionAlertMessage>(null, Event.SHOW_PROMPT_INJECTION_ALERT, new(results));
    }

    /// <summary>
    /// Reports everything filtered during one user action when it goes out of scope.
    /// </summary>
    public sealed class ReportingScope(PromptInjectionGuardService guardService) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await guardService.EndActionAsync();
    }
}