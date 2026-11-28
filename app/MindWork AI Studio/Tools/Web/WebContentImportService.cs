using AIStudio.AIOperations;
using AIStudio.AIOperations.TextContentCleaning;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Imports;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.Web;

/// <summary>
/// Loads web content for the web content readers of assistants, in the background.
/// </summary>
/// <remarks>
/// A reader goes away with its assistant when the user leaves it, while loading and above all cleaning a page
/// can take a minute. So the work runs here, keyed by the field it fills, see ImportTarget, and its result
/// waits until the reader of that field is there again, see TryTakeResult. This follows the transcription of
/// media files, see MediaTranscriptionService, with one difference: there is no common queue. Each field has
/// its own import, so several assistants, or several readers of an assistant plugin, load at the same time.
/// Only the same field cannot start a second import while its first one runs.
/// </remarks>
public sealed class WebContentImportService(
    WebPageRetrievalService webPageRetrievalService,
    PromptInjectionGuardService promptInjectionGuardService,
    TextContentCleaningOperation textContentCleaning,
    AIOperationProviderResolver providerResolver,
    SettingsManager settingsManager,
    ILogger<WebContentImportService> logger)
{
    /// <summary>
    /// How long loading one page may take.
    /// </summary>
    /// <remarks>
    /// The user waits for this text, so it is shorter than what the tools allow themselves for the
    /// pages they fetch.
    /// </remarks>
    private const int TIMEOUT_SECONDS = 60;

    /// <summary>Protects the running imports and the waiting results.</summary>
    private readonly Lock stateLock = new();

    /// <summary>The state of each running import.</summary>
    private readonly Dictionary<ImportTarget, WebContentImportSnapshot> runningImports = [];

    /// <summary>Results waiting for the reader of their field.</summary>
    private readonly Dictionary<ImportTarget, WebContentImportResult> results = [];

    /// <summary>Raised with the owner whose imports changed: one started, went on to its next step, or ended.</summary>
    public event Action<ImportOwner>? StateChanged;

    /// <summary>
    /// What the reader adds to the choice of the provider for the content cleaner.
    /// </summary>
    /// <remarks>
    /// Without a model of its own, the cleaner falls back on the model of the assistant, and then on
    /// the app-wide default. Its minimum confidence applies to both of them: a model somebody picked
    /// for the assistant has to clear the bar of the cleaner before the cleaner uses it.
    /// </remarks>
    /// <param name="settingsManager">The settings, for the app-wide default.</param>
    /// <param name="assistantProvider">The model of the assistant around the reader.</param>
    /// <returns>The options for the provider resolver.</returns>
    internal static AIOperationProviderOptions GetCleanerProviderOptions(SettingsManager settingsManager, AIStudio.Settings.Provider assistantProvider) => new()
    {
        FallbackProviderIds = [assistantProvider.Id, settingsManager.AppDefaultProviderId],
        OwnMinimumConfidenceAppliesToFallbacks = true,
    };

    /// <summary>Gets whether one of the fields of an owner is loading web content.</summary>
    /// <param name="owner">The owner, e.g., an assistant.</param>
    /// <returns><c>true</c> while an import of the owner runs.</returns>
    public bool IsBusy(ImportOwner owner)
    {
        lock (this.stateLock)
            return this.runningImports.Keys.Any(target => target.Owner == owner);
    }

    /// <summary>Gets the state of the import which fills a field, while it runs.</summary>
    /// <param name="target">The field.</param>
    /// <returns>A copy of the state, or <c>null</c> when no import of the field runs.</returns>
    public WebContentImportSnapshot? GetSnapshot(ImportTarget target)
    {
        lock (this.stateLock)
            return this.runningImports.GetValueOrDefault(target);
    }

    /// <summary>Gets copies of the states of all running imports, e.g., for the navigation.</summary>
    public IReadOnlyCollection<WebContentImportSnapshot> GetSnapshots()
    {
        lock (this.stateLock)
            return [.. this.runningImports.Values];
    }

    /// <summary>
    /// Starts loading web content into a field, unless an import of that field still runs.
    /// </summary>
    /// <param name="target">The field to fill.</param>
    /// <param name="request">What to load.</param>
    /// <returns><c>true</c> when the import started.</returns>
    public bool TryStart(ImportTarget target, WebContentImportRequest request) => this.Start(target, request, reportStep => this.ImportAsync(request, reportStep)) is not null;

    /// <summary>
    /// Starts an import into a field, unless an import of that field still runs.
    /// </summary>
    /// <remarks>
    /// The import runs on the thread pool, not on the circuit of the reader which started it: the reader may
    /// go away any moment. A new import replaces a result which its reader did not take yet. Tests hand in an
    /// import of their own.
    /// </remarks>
    /// <param name="target">The field to fill.</param>
    /// <param name="request">What the import loads, for its state and for the result of an import which failed unexpectedly.</param>
    /// <param name="import">The work, which reports each step it reaches.</param>
    /// <returns>The running import, or <c>null</c> when an import of the field still runs.</returns>
    internal Task? Start(ImportTarget target, WebContentImportRequest request, Func<Action<ReadWebContentSteps>, Task<WebContentImportResult>> import)
    {
        lock (this.stateLock)
        {
            if (!this.runningImports.TryAdd(target, new(target, request.Url, request.CleanContent, ReadWebContentSteps.LOADING)))
                return null;

            this.results.Remove(target);
        }

        this.NotifyStateChanged(target.Owner);
        return Task.Run(() => this.RunAsync(target, request.Url, import));
    }

    /// <summary>
    /// Takes the result which waits for a field, so that it is delivered exactly once.
    /// </summary>
    /// <param name="target">The field.</param>
    /// <returns>The result, or <c>null</c> when none waits.</returns>
    public WebContentImportResult? TryTakeResult(ImportTarget target)
    {
        lock (this.stateLock)
            return this.results.Remove(target, out var result) ? result : null;
    }

    /// <summary>
    /// Discards the results which wait for the fields of an owner, e.g., when the user resets the assistant.
    /// </summary>
    /// <remarks>
    /// A running import is left alone, as with media files: the assistant does not reset while one runs.
    /// </remarks>
    /// <param name="owner">The owner.</param>
    public void ClearOwnerState(ImportOwner owner)
    {
        lock (this.stateLock)
        {
            foreach (var target in this.results.Keys.Where(target => target.Owner == owner).ToList())
                this.results.Remove(target);
        }

        this.NotifyStateChanged(owner);
    }

    private async Task RunAsync(ImportTarget target, string url, Func<Action<ReadWebContentSteps>, Task<WebContentImportResult>> import)
    {
        WebContentImportResult result;
        try
        {
            result = await import(step => this.ReportStep(target, step));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not load the web content from '{ProvidedUrl}'.", url);
            result = new(url, string.Empty, WebContentImportIssue.LOAD_FAILED, exception.Message);
        }

        lock (this.stateLock)
        {
            this.runningImports.Remove(target);
            this.results[target] = result;
        }

        this.NotifyStateChanged(target.Owner);
    }

    private void ReportStep(ImportTarget target, ReadWebContentSteps step)
    {
        lock (this.stateLock)
        {
            if (!this.runningImports.TryGetValue(target, out var snapshot))
                return;

            this.runningImports[target] = snapshot with { Step = step };
        }

        this.NotifyStateChanged(target.Owner);
    }

    /// <summary>
    /// Loads a page, filters it for prompt injections, and cleans it if asked to.
    /// </summary>
    /// <remarks>
    /// Never throws: the content which arrived before an error goes into the result, together with the error.
    /// </remarks>
    private async Task<WebContentImportResult> ImportAsync(WebContentImportRequest request, Action<ReadWebContentSteps> reportStep)
    {
        var markdown = string.Empty;
        try
        {
            //
            // The same retrieval the read web page tool uses, so a page is fetched and read one
            // way throughout AI Studio. The difference is the target policy: here the user typed
            // the URL, so their own network is not off limits.
            //
            var retrievedPage = await webPageRetrievalService.RetrieveAsync(
                new Uri(request.Url),
                new WebPageRetrievalOptions
                {
                    TimeoutSeconds = TIMEOUT_SECONDS,
                    TargetChosenByUser = true,
                });

            reportStep(ReadWebContentSteps.PARSING);
            markdown = await promptInjectionGuardService.SanitizeAsync(retrievedPage.ExtractedPage.Markdown, PromptInjectionSource.WebContent(request.Url));
            if (!request.CleanContent)
                return new(request.Url, markdown);

            var cleaningRequest = new TextContentCleaningRequest(markdown, request.Url);
            var choice = providerResolver.Resolve(textContentCleaning, cleaningRequest, TextContentCleaningOperation.GetProviderSettings(settingsManager), GetCleanerProviderOptions(settingsManager, request.AssistantProvider));
            if (!choice.HasProvider)
                return new(request.Url, markdown, WebContentImportIssue.NOT_CLEANED_NO_MODEL);

            reportStep(ReadWebContentSteps.CLEANING);
            var cleaningResult = await textContentCleaning.RunAsync(cleaningRequest, new(choice.Provider));
            return new(request.Url, cleaningResult.Markdown, cleaningResult.IsCleaned ? WebContentImportIssue.NONE : WebContentImportIssue.NOT_CLEANED_NO_ANSWER);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not load the web content from '{ProvidedUrl}'.", request.Url);
            return new(request.Url, markdown, WebContentImportIssue.LOAD_FAILED, exception.Message);
        }
    }

    /// <summary>
    /// Tells every handler about a change, so that a handler which throws keeps none of the others from hearing about it.
    /// </summary>
    private void NotifyStateChanged(ImportOwner owner)
    {
        if (this.StateChanged is not { } stateChanged)
            return;

        foreach (var @delegate in stateChanged.GetInvocationList())
        {
            try
            {
                ((Action<ImportOwner>)@delegate)(owner);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "A handler failed while it was told about a changed web content import.");
            }
        }
    }
}