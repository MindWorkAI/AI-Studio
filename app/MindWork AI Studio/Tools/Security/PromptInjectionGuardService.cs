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
/// user sees.<br/><br/>
/// The reporting works per user action, cf. BeginAction: each action tells the user once, when
/// it ends, about what was filtered for it. One service serves the whole app, so the action has
/// to travel with the content instead of being kept here.
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
    /// <param name="reportingScope">The action the content belongs to, or null when it belongs to none.</param>
    /// <returns>The content with any suspicious passages removed.</returns>
    public async Task<string> SanitizeAsync(string text, PromptInjectionSource source, PromptInjectionReportingScope? reportingScope = null)
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
            await this.ReportAsync(new(source, response.Findings, response.RedactedCount), reportingScope);

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
    /// <param name="reportingScope">The action the contents belong to, or null when they belong to none.</param>
    /// <returns>The contents with any suspicious passages removed, in the order they came in.</returns>
    public async Task<IReadOnlyList<string>> SanitizeAsync(IReadOnlyList<PromptInjectionText> texts, PromptInjectionReportingScope? reportingScope = null)
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
        // both filtered is one thing that happened to the user, not two. Doing so here rather than
        // in the report keeps both counts: two texts with the same findings would look like one
        // text filtered twice there.
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
        await using var batchScope = this.BeginAction(within: reportingScope);
        foreach (var (source, aggregate) in findingsBySource)
            await this.ReportAsync(new(source, aggregate.Findings, aggregate.RedactedCount), batchScope);

        return sanitizedTexts;
    }

    /// <summary>
    /// Records what was filtered out of one piece of content and tells the user about it.
    /// </summary>
    /// <remarks>
    /// Within an action, the result is collected and reported together with the rest of that
    /// action once it ends. Without one it is reported right away, and so is a result whose
    /// action has ended already: waiting for the next action would either never reach the user,
    /// or reach them as part of an unrelated action later on.<br/><br/>
    /// Only the action passed in counts, never what else is running. A run over a large data
    /// source takes minutes, and a message sent meanwhile must not wait for it, nor end up in the
    /// same report.
    /// </remarks>
    /// <param name="result">What was filtered out of the content.</param>
    /// <param name="reportingScope">The action the content belongs to, or null when it belongs to none.</param>
    public async Task ReportAsync(PromptInjectionScanResult result, PromptInjectionReportingScope? reportingScope = null)
    {
        if (!result.WasFiltered)
            return;

        lock (this.reportLock)
        {
            if (FindCollectingScope(reportingScope) is { } collectingScope)
            {
                collectingScope.PendingResults.Add(result);
                return;
            }
        }

        await this.ReportNowAsync([result]);
    }

    /// <summary>
    /// Marks the start of one user action, such as attaching a batch of files or sending a
    /// message.
    /// </summary>
    /// <remarks>
    /// Results are collected until the action finishes, so the user gets one report about
    /// twenty documents instead of twenty reports. An action begun within another one is part
    /// of it, and only the outermost one reports. Unrelated actions report on their own, each
    /// as soon as it ends, however much they overlap.<br/><br/>
    /// The scope has to travel with the content: hand it to wherever that content is read or
    /// filtered, such as ReportAsync, SanitizeAsync, or RustService.ReadArbitraryFileData.
    /// </remarks>
    /// <param name="within">The action this one is part of, or null when it is an action of its own.</param>
    /// <returns>A scope that reports what was filtered once it is disposed.</returns>
    public PromptInjectionReportingScope BeginAction(PromptInjectionReportingScope? within = null) => new(this, within);

    /// <summary>
    /// Ends an action and reports what was collected for it.
    /// </summary>
    /// <remarks>
    /// A scope within an action which still runs has collected nothing, since its results went to
    /// the outermost scope. It collects only once the outer one has ended before it, e.g., for work
    /// which outlived the action that started it. Ending a scope a second time does nothing.
    /// </remarks>
    /// <param name="scope">The scope which ends.</param>
    internal async Task EndActionAsync(PromptInjectionReportingScope scope)
    {
        List<PromptInjectionScanResult> results;
        lock (this.reportLock)
        {
            if (!scope.IsOpen)
                return;

            scope.IsOpen = false;
            results = [..scope.PendingResults];
            scope.PendingResults.Clear();
        }

        if (results.Count > 0)
            await this.ReportNowAsync(results);
    }

    /// <summary>
    /// Finds the scope which collects a result of the given one: the outermost scope of its action
    /// which is still running.
    /// </summary>
    /// <remarks>
    /// Call under the report lock only.
    /// </remarks>
    /// <param name="scope">The scope the result was reported for, or null.</param>
    /// <returns>The collecting scope, or null when the result has to be reported right away.</returns>
    private static PromptInjectionReportingScope? FindCollectingScope(PromptInjectionReportingScope? scope)
    {
        PromptInjectionReportingScope? collectingScope = null;
        for (; scope is not null; scope = scope.Parent)
        {
            if (scope.IsOpen)
                collectingScope = scope;
        }

        return collectingScope;
    }

    /// <summary>
    /// Combines the results of one report into one result per source, in the order the sources came in.
    /// </summary>
    /// <remarks>
    /// An action may filter the same content twice: the classic data source search filters a passage when
    /// it checks whether the passage fits the question, and once more when it hands the passage to the
    /// model. Both times, the result is the same, so a result equal to one already seen for its source is
    /// dropped; the user hears about the passage once. The findings carry no position, though, so two
    /// passages of one file with exactly the same findings count as one as well. That mostly happens
    /// where two neighboring chunks overlap, which is the same passage indeed.<br/><br/>
    /// Other results of one source, such as two passages of one file or the mails of one mailbox,
    /// become a single result with all their findings and the sum of the removed passages. The report
    /// counts sources, and one file is one source, however many of its passages were filtered.
    /// </remarks>
    /// <param name="results">The results of one report.</param>
    /// <returns>One result per source.</returns>
    private static IReadOnlyList<PromptInjectionScanResult> CombineBySource(IReadOnlyList<PromptInjectionScanResult> results)
    {
        if (results.Count < 2)
            return results;

        return results.GroupBy(result => result.Source).Select(CombineOneSource).ToList();

        static PromptInjectionScanResult CombineOneSource(IGrouping<PromptInjectionSource, PromptInjectionScanResult> resultsOfSource)
        {
            List<PromptInjectionScanResult> distinctResults = [];
            foreach (var result in resultsOfSource)
            {
                if (!distinctResults.Any(known => known.RedactedCount == result.RedactedCount && known.Findings.SequenceEqual(result.Findings)))
                    distinctResults.Add(result);
            }

            return distinctResults.Count is 1
                ? distinctResults[0]
                : new(resultsOfSource.Key, distinctResults.SelectMany(result => result.Findings).ToList(), distinctResults.Sum(result => result.RedactedCount));
        }
    }

    private async Task ReportNowAsync(IReadOnlyList<PromptInjectionScanResult> reportedResults)
    {
        var results = CombineBySource(reportedResults);
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
}