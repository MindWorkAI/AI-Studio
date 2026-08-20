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
    ILogger<PromptInjectionGuardService> logger)
{
    public const string WIKI_URL = "https://en.wikipedia.org/wiki/Prompt_engineering#Prompt_injection";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PromptInjectionGuardService).Namespace, nameof(PromptInjectionGuardService));

    private readonly Lock reportLock = new();
    private readonly List<PromptInjectionScanResult> pendingResults = [];
    private int openActions;

    public bool IsProtectionEnabled => settingsManager.ConfigurationData.Chat.EnablePromptInjectionProtection;

    /// <summary>
    /// Filters prompt injections out of a text the runtime did not read itself, such as a web
    /// page or a retrieval context.
    /// </summary>
    /// <remarks>
    /// Returns usable text in every case. When filtering is off, or the runtime cannot be
    /// reached, the text is passed through unchanged: refusing the user's content because a
    /// check could not run would cost them their work over a check that is best-effort anyway.
    /// The failure is logged and shown, so it does not pass silently.
    /// </remarks>
    /// <param name="text">The content to filter.</param>
    /// <param name="source">Where the content came from, for the report shown to the user.</param>
    /// <returns>The content with any suspicious passages removed.</returns>
    public async Task<string> SanitizeAsync(string text, PromptInjectionSource source)
    {
        if (!this.IsProtectionEnabled || string.IsNullOrWhiteSpace(text))
            return text;

        if (await rustService.SanitizePromptInjections(text) is not { } response)
        {
            logger.LogError("Could not check {SourceKind} '{SourceLabel}' for prompt injections. The content is used unchanged.", source.Kind, source.Label);
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.GppMaybe,
                string.Format(TB("AI Studio could not check '{0}' for prompt injections. The content is used as it is."), source.Label)));

            return text;
        }

        if (response.RedactedCount > 0)
            await this.ReportAsync(new(source, response.Findings, response.RedactedCount));

        return response.SanitizedText;
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

        logger.LogWarning(
            "Filtered {Count} suspected prompt injection(s) in {SourceKind} '{SourceLabel}'. RuleIds={RuleIds}",
            result.RedactedCount,
            result.Source.Kind,
            result.Source.Label,
            string.Join(", ", result.RuleIds));

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
        await MessageBus.INSTANCE.SendWarning(new(
            Icons.Material.Filled.GppMaybe,
            results.Count is 1
                ? string.Format(TB("AI Studio removed suspicious instructions from '{0}' before using it."), results[0].Source.Label)
                : string.Format(TB("AI Studio removed suspicious instructions from {0} sources before using them."), results.Count)));

        logger.LogInformation("Reported {Count} filtered prompt injection(s) across {Sources} source(s) to the user.", totalCount, results.Count);

        if (settingsManager.ConfigurationData.Chat.ShowPromptInjectionAlert)
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