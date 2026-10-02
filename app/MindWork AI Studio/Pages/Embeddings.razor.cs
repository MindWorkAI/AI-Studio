using System.Globalization;

using AIStudio.Components;
using AIStudio.Dialogs.Settings;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

public partial class Embeddings : MSGComponentBase
{
    private static readonly int[] PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<Embeddings> Logger { get; init; } = null!;

    private IReadOnlyList<DataSourceEmbeddingStatus> Statuses { get; set; } = [];

    private string? expandedDataSourceId;
    private bool userChoseExpansion;

    /// <remarks>
    /// The language of AI Studio is chosen in its settings and does not move the thread's culture
    /// along with it. Without this, a German reading a German page would find a file count written
    /// with English separators.
    /// </remarks>
    private CultureInfo currentCulture = CultureInfo.InvariantCulture;

    /// <remarks>
    /// The sums above every data source count files and mails alike, which is why their chips name
    /// neither.
    /// </remarks>
    private int TotalIndexedDocuments => this.Statuses.Sum(status => status.IndexedDocuments);

    private int TotalPendingDocuments => this.Statuses.Sum(status => Math.Max(0, status.TotalDocuments - status.IndexedDocuments - status.FailedDocuments - status.PermanentlySkippedDocuments));

    private int TotalFailedDocuments => this.Statuses.Sum(status => status.FailedDocuments);

    private int TotalPermanentlySkippedDocuments => this.Statuses.Sum(status => status.PermanentlySkippedDocuments);

    private bool AreMailboxesEnabled => PreviewFeatures.PRE_MAILBOXES_2026.IsEnabled(this.SettingsManager);

    /// <remarks>
    /// The chips above count documents, which says nothing about how far the list of data sources
    /// itself has come. While several of them wait their turn, this is the one line saying so. With a
    /// single data source there is nothing to say: its own row already tells the whole story.
    /// </remarks>
    private bool IsWorkingThroughDataSources => this.Statuses.Count > 1 && this.Statuses.Any(status => status.State is DataSourceEmbeddingState.RUNNING or DataSourceEmbeddingState.QUEUED);

    /// <remarks>
    /// The one being worked on is the one after those which are done. A data source which needs
    /// attention counts as done here: nothing is going to happen to it during this pass.
    /// </remarks>
    private int CurrentDataSourceNumber => Math.Min(this.Statuses.Count, this.Statuses.Count(status => status.State is DataSourceEmbeddingState.COMPLETED or DataSourceEmbeddingState.FAILED) + 1);

    protected override async Task OnInitializedAsync()
    {
        //
        // This page belongs to the local RAG preview feature. Unlike the other preview pages, it
        // has a route of its own, so it can be reached by typing the address even while the feature
        // is switched off. There is nothing to show in that case.
        //
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
        {
            this.NavigationManager.NavigateTo(Routes.HOME);
            return;
        }

        this.ApplyFilters([], [ Event.RAG_EMBEDDING_STATUS_CHANGED, Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED ]);
        await this.RefreshCulture();
        await base.OnInitializedAsync();
        this.ReloadStatuses();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            await this.RefreshCulture();

        if (triggeredEvent is Event.RAG_EMBEDDING_STATUS_CHANGED or Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
        {
            this.ReloadStatuses();
            this.StateHasChanged();
        }
    }

    private async Task RefreshCulture()
    {
        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCulture = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);
    }

    private void ReloadStatuses()
    {
        this.Statuses = this.DataSourceEmbeddingService
            .GetStatuses()
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.DataSourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        this.UpdateAutoExpansion();
    }

    /// <summary>
    /// Opens the data source which is worth reading, as long as the user has not chosen one.
    /// </summary>
    /// <remarks>
    /// It never closes what is open. A data source which finishes its run while somebody is reading
    /// it would otherwise fold up at the very moment its result becomes interesting. From the first
    /// click on, the page stops rearranging itself at all.
    /// </remarks>
    private void UpdateAutoExpansion()
    {
        if (this.userChoseExpansion)
            return;

        // The list is sorted by state, so the first match is the most pressing one: a running data
        // source before a queued one, and a failed one before a completed one:
        var worthOpening = this.Statuses.FirstOrDefault(IsWorthOpening);
        if (worthOpening is null)
            return;

        this.expandedDataSourceId = worthOpening.DataSourceId;
    }

    /// <remarks>
    /// Whoever opens this page does so because a run is under way or because something went wrong.
    /// Meeting nothing but closed panels would be a step back from the version which showed every
    /// data source at once.
    /// </remarks>
    private static bool IsWorthOpening(DataSourceEmbeddingStatus status) =>
        status.State is DataSourceEmbeddingState.RUNNING or DataSourceEmbeddingState.QUEUED or DataSourceEmbeddingState.FAILED || status.FailedDocuments > 0;

    /// <remarks>
    /// MudBlazor keeps track of which panel is open on its own, so this only records the decision.
    /// Both events of a switch arrive, in either order — the one closing the old panel and the one
    /// opening the new one — which is why the closing event only clears what it actually named.
    /// </remarks>
    private void DataSourcePanelExpandedChanged(DataSourceEmbeddingStatus status, bool isExpanded)
    {
        this.userChoseExpansion = true;

        if (isExpanded)
            this.expandedDataSourceId = status.DataSourceId;
        else if (this.expandedDataSourceId == status.DataSourceId)
            this.expandedDataSourceId = null;
    }

    /// <summary>
    /// Opens the data source settings, the same dialog the chat offers next to its data source selection.
    /// </summary>
    /// <remarks>
    /// Nothing is left to do once it closes: the dialog writes the settings itself and publishes
    /// CONFIGURATION_CHANGED, which this page already listens to.
    /// </remarks>
    private async Task OpenDataSourceSettings()
    {
        var dialogParameters = new DialogParameters();
        var dialogReference = await this.DialogService.ShowAsync<SettingsDialogDataSources>(null, dialogParameters, DialogOptions.FULLSCREEN);
        await dialogReference.Result;
    }

    /// <summary>
    /// What the panel of a data source says about its progress through its files or mails.
    /// </summary>
    /// <remarks>
    /// While a file is being worked on, the sentence names that file and how far into it we are.
    /// Counting finished files alone leaves the same sentence standing for hours on a document of
    /// several thousand pages, and a progress which never moves cannot be told apart from one which
    /// is stuck. The total number of blocks is not part of it: the blocks are produced while the
    /// file is read, so nobody knows how many there will be until the file is done.
    ///
    /// Which sentence is shown depends on the file, not on the block. A file has no blocks yet
    /// while it is being read, and hanging the choice on the block number let the line jump back
    /// and forth between two entirely different sentences at every file. Now the beginning of the
    /// sentence stays put and the blocks are appended to it as soon as the first one arrives.
    ///
    /// A mail has no pages: those of its attachments would not say which attachment they are in.
    /// </remarks>
    private string GetProgressText(DataSourceEmbeddingStatus status)
    {
        var isMailbox = IsMailbox(status);
        if (status.State is not DataSourceEmbeddingState.RUNNING || string.IsNullOrWhiteSpace(status.CurrentDocument))
        {
            return isMailbox
                ? string.Format(T("{0} of {1} mails are indexed."), this.FormatNumber(status.IndexedDocuments), this.FormatNumber(status.TotalDocuments))
                : string.Format(T("{0} of {1} files are indexed."), this.FormatNumber(status.IndexedDocuments), this.FormatNumber(status.TotalDocuments));
        }

        //
        // Everything already dealt with, plus the one in hand. Skipped and failed files are part of
        // that: they are behind us in the folder, and leaving them out would let the number fall
        // behind the file whose name is shown right next to it.
        //
        var currentNumber = this.FormatNumber(Math.Min(status.TotalDocuments, status.IndexedDocuments + status.PermanentlySkippedDocuments + status.FailedDocuments + 1));
        var total = this.FormatNumber(status.TotalDocuments);
        if (isMailbox)
        {
            return status.CurrentDocumentBlock is { } mailBlock
                ? string.Format(T("Mail {0} of {1} is being indexed: block {2}."), currentNumber, total, this.FormatNumber(mailBlock))
                : string.Format(T("Mail {0} of {1} is being indexed."), currentNumber, total);
        }

        return status switch
        {
            { CurrentDocumentBlock: { } block, CurrentDocumentPage: { } page } => string.Format(T("File {0} of {1} is being indexed: block {2}, page {3}."), currentNumber, total, this.FormatNumber(block), this.FormatNumber(page)),
            { CurrentDocumentBlock: { } block } => string.Format(T("File {0} of {1} is being indexed: block {2}."), currentNumber, total, this.FormatNumber(block)),
            _ => string.Format(T("File {0} of {1} is being indexed."), currentNumber, total),
        };
    }

    private string GetSkippedText(DataSourceEmbeddingStatus status) => IsMailbox(status)
        ? string.Format(T("Skipped mails: {0}."), this.FormatNumber(status.PermanentlySkippedDocuments))
        : string.Format(T("Skipped files: {0}. AI Studio reads them again once they change."), this.FormatNumber(status.PermanentlySkippedDocuments));

    private string GetFailedText(DataSourceEmbeddingStatus status) => IsMailbox(status)
        ? string.Format(T("Failed mails: {0}"), this.FormatNumber(status.FailedDocuments))
        : string.Format(T("Failed files: {0}"), this.FormatNumber(status.FailedDocuments));

    private string GetCurrentDocumentText(DataSourceEmbeddingStatus status) => IsMailbox(status)
        ? string.Format(T("Current mail: {0}"), status.CurrentDocument)
        : string.Format(T("Current file: {0}"), status.CurrentDocument);

    private static bool IsMailbox(DataSourceEmbeddingStatus status) => status.DataSourceType is DataSourceType.MAILBOX;

    private string FormatNumber(int value) => value.ToString("N0", this.currentCulture);

    private static Color GetStatusColor(DataSourceEmbeddingStatus status) => status.State switch
    {
        DataSourceEmbeddingState.RUNNING => Color.Warning,
        DataSourceEmbeddingState.QUEUED => Color.Info,
        DataSourceEmbeddingState.FAILED => Color.Error,
        DataSourceEmbeddingState.COMPLETED when status.FailedDocuments > 0 => Color.Warning,
        DataSourceEmbeddingState.COMPLETED => Color.Success,
        _ => Color.Default,
    };

    /// <summary>
    /// What a group of failures has in common.
    /// </summary>
    /// <remarks>
    /// Also the key the failures are grouped by: two of them belong together exactly when the
    /// list would say the same thing about both.
    /// </remarks>
    private sealed record FailureCause(int Priority, string Title, string Icon, Color Color, bool IsPermanent, bool NeedsProviderSettings, bool ShowsMessagePerFile);

    private sealed record FailureGroup(FailureCause Cause, string EmbeddingProviderName, IReadOnlyList<DataSourceEmbeddingFailure> Failures);

    /// <summary>
    /// Puts the failures of a data source into one group per cause, the most pressing one first.
    /// </summary>
    /// <remarks>
    /// Within a priority, the largest group comes first: it is the one telling the user the most
    /// about their folder.
    /// </remarks>
    private IReadOnlyList<FailureGroup> GetFailureGroups(DataSourceEmbeddingStatus status) => status.Failures
        .GroupBy(this.GetFailureCause)
        .Select(group => new FailureGroup(group.Key, GetEmbeddingProviderName(group), group.OrderBy(failure => IsMailbox(status) ? GetMailName(failure) : failure.DocumentKey, StringComparer.OrdinalIgnoreCase).ToList()))
        .OrderBy(group => group.Cause.Priority)
        .ThenByDescending(group => group.Failures.Count)
        .ThenBy(group => group.Cause.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private FailureCause GetFailureCause(DataSourceEmbeddingFailure failure)
    {
        //
        // Anything the provider answered comes first: it stops the entire data source, while a
        // file nobody can read costs that one file:
        //
        if (failure.FailureReason is not ProviderRequestFailureReason.NONE)
        {
            var isFixedInSettings = failure.FailureReason.IsFixedInProviderSettings();
            return new FailureCause(isFixedInSettings ? 0 : 1, failure.FailureReason.GetName(), isFixedInSettings ? Icons.Material.Filled.Key : Icons.Material.Filled.CloudOff, Color.Error, false, isFixedInSettings, true);
        }

        //
        // Codes without a name of their own carry everything they know in the message of the
        // single file, which is why those groups show that message per file:
        //
        var causeName = failure.ExtractionCode.GetIndexingCauseName();
        var hasCauseName = !string.IsNullOrWhiteSpace(causeName);
        var title = hasCauseName ? causeName : T("Other cause");

        return failure.IsPermanent
            ? new FailureCause(3, title, Icons.Material.Filled.SkipNext, Color.Default, true, false, !hasCauseName)
            : new FailureCause(2, title, Icons.Material.Filled.ReportProblem, Color.Warning, false, false, !hasCauseName);
    }

    private static string GetEmbeddingProviderName(IEnumerable<DataSourceEmbeddingFailure> failures) => failures
        .Select(failure => failure.EmbeddingProviderName)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;

    private static string GetGroupHeader(FailureGroup group) => $"{group.Cause.Title} ({group.Failures.Count})";

    private static string GetFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return string.IsNullOrWhiteSpace(fileName) ? filePath : fileName;
    }

    private static string GetOccurrenceText(DataSourceEmbeddingFailure failure) => failure.OccurredAtUtc > DateTimeOffset.MinValue ? failure.OccurredAtUtc.ToLocalTime().ToString("g") : string.Empty;

    /// <remarks>
    /// A failure which was not about one file, such as a folder which is gone, carries the name of
    /// the data source instead of a path. There is nothing to show for those, nor for a mail, which
    /// lies on a server.
    /// </remarks>
    private static bool CanShowInFileManager(DataSourceEmbeddingStatus status, DataSourceEmbeddingFailure failure) => !IsMailbox(status) && !string.IsNullOrWhiteSpace(failure.DocumentKey) && Path.IsPathRooted(failure.DocumentKey);

    /// <summary>
    /// How a failed mail is called in the list: by its subject, which is all the user knows it by.
    /// </summary>
    /// <remarks>
    /// The key of a mail is a hash, which means nothing to anybody. A failure which was not about
    /// one mail carries the name of the mailbox instead.
    /// </remarks>
    private static string GetMailName(DataSourceEmbeddingFailure failure) => string.IsNullOrWhiteSpace(failure.DisplayName) ? failure.DocumentKey : failure.DisplayName;

    /// <summary>
    /// Opens the file browser of the system and selects the file in it.
    /// </summary>
    /// <remarks>
    /// Reading that a file could not be indexed is where the work starts, not where it ends: the
    /// file has to be opened, replaced, or run through an OCR. This is the same way out the log
    /// viewer offers for the log files.
    /// </remarks>
    private async Task ShowInFileManager(DataSourceEmbeddingFailure failure)
    {
        OpenPathResponse response;
        try
        {
            response = await this.RustService.TryOpenPathInRuntimeFileManager(failure.DocumentKey);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not show a file of the embedding failure list in the file manager.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, T("Could not open the file location.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, string.Format(T("Could not open the file location: {0}"), issue)));
    }

    /// <remarks>
    /// An unreadable index is left to the repair button below: another attempt would open the same
    /// store and fail the same way, so offering both would be offering one that does nothing.
    /// </remarks>
    private bool CanRefresh(DataSourceEmbeddingStatus status)
    {
        return this.DataSourceEmbeddingService.CanRefreshDataSource(status.DataSourceId) &&
               status is { VectorStoreUnreadable: false, State: not DataSourceEmbeddingState.RUNNING and not DataSourceEmbeddingState.QUEUED } &&
               (status.State is DataSourceEmbeddingState.FAILED || status.FailedDocuments > 0);
    }

    /// <remarks>
    /// Offered for the one failure which no further attempt gets past. It is a button of its own
    /// and not the refresh one, because what it does is not what the user expects of a refresh:
    /// everything indexed so far is thrown away and paid for again.
    /// </remarks>
    private bool CanRepair(DataSourceEmbeddingStatus status)
    {
        return this.DataSourceEmbeddingService.CanRefreshDataSource(status.DataSourceId) &&
            status is { State: DataSourceEmbeddingState.FAILED, VectorStoreUnreadable: true };
    }

    /// <summary>
    /// Takes the user to the settings, where the embedding providers are configured.
    /// </summary>
    /// <remarks>
    /// Offered only for the failures a setting fixes, such as a rejected API key. Reading what
    /// went wrong and then having to find the right page is where people give up.
    /// </remarks>
    private void OpenEmbeddingProviderSettings() => this.NavigationManager.NavigateTo(Routes.SETTINGS);

    private async Task RefreshDataSource(DataSourceEmbeddingStatus status)
    {
        await this.DataSourceEmbeddingService.RetryDataSourceAsync(status.DataSourceId);
        this.ReloadStatuses();
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task RepairDataSource(DataSourceEmbeddingStatus status)
    {
        if (!await DataSourceRepair.ConfirmAndRepairAsync(this.DialogService, this.DataSourceEmbeddingService, status.DataSourceId, status.DataSourceName))
            return;

        this.ReloadStatuses();
        await this.InvokeAsync(this.StateHasChanged);
    }
}
